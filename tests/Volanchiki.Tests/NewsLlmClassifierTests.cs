using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Volanchiki.Api.Data;
using Volanchiki.Api.Llm;
using Volanchiki.Api.News;
using Xunit;
using static Volanchiki.Tests.TestData;

namespace Volanchiki.Tests;

// Настоящие LlmClient и NewsLlmClassifier; подменён только HTTP-канал до шлюза,
// чтобы без сети проверить, что мы отправляем и как разбираем ответ модели.
public class NewsLlmClassifierTests
{
    sealed class FakeGateway : HttpMessageHandler
    {
        public List<string> RequestBodies { get; } = new();
        public string? ModelContent { get; set; } = "[]";
        public HttpStatusCode Status { get; set; } = HttpStatusCode.OK;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            RequestBodies.Add(request.Content == null ? "" : await request.Content.ReadAsStringAsync(ct));

            var body = Status == HttpStatusCode.OK
                ? JsonSerializer.Serialize(new { choices = new[] { new { message = new { content = ModelContent } } } })
                : "{\"error\":\"boom\"}";
            return new HttpResponseMessage(Status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        }
    }

    static (NewsLlmClassifier Classifier, NewsStore Store, FakeGateway Gateway) Make(string apiKey = "test-key")
    {
        var gateway = new FakeGateway();
        var http = new HttpClient(gateway) { BaseAddress = new Uri("http://fake-gateway/v1/") };
        var opt = Microsoft.Extensions.Options.Options.Create(new LlmOptions { ApiKey = apiKey, ModelLight = "light-model-test" });
        var llm = new LlmClient(http, opt, NullLogger<LlmClient>.Instance);
        var store = new NewsStore();
        return (new NewsLlmClassifier(llm, store, NullLogger<NewsLlmClassifier>.Instance), store, gateway);
    }

    static void AddPending(NewsStore s, params string[] ids) =>
        s.UpsertMany(ids.Select((id, i) =>
            News(id, "Заголовок " + id, published: DateTimeOffset.UtcNow.AddMinutes(-i), needsLlm: true)));

    static string Answer(params (string Id, string Category, bool Business)[] items) =>
        JsonSerializer.Serialize(items.Select(x => new { id = x.Id, category = x.Category, business = x.Business }));

    [Fact]
    public async Task Applies_valid_answers_and_clears_the_queue()
    {
        var (c, store, gw) = Make();
        AddPending(store, "a", "b");
        gw.ModelContent = Answer(("a", "Нефть", true), ("b", "Прочее", false));

        var applied = await c.ClassifyPendingAsync(10, CancellationToken.None);

        Assert.Equal(2, applied);
        Assert.Equal("Нефть", store.Get("a")!.Category);
        Assert.Equal("llm", store.Get("a")!.ClassificationSource);
        Assert.Equal("Прочее", store.Get("b")!.Category);
        Assert.Equal(0, store.PendingLlmCount);
    }

    [Fact]
    public async Task Sends_the_light_model_and_the_news_titles()
    {
        var (c, store, gw) = Make();
        AddPending(store, "a");
        gw.ModelContent = Answer(("a", "Нефть", true));

        await c.ClassifyPendingAsync(10, CancellationToken.None);

        Assert.Single(gw.RequestBodies);
        using var req = JsonDocument.Parse(gw.RequestBodies[0]);
        Assert.Equal("light-model-test", req.RootElement.GetProperty("model").GetString());
        var userMsg = req.RootElement.GetProperty("messages")[1].GetProperty("content").GetString()!;
        using var items = JsonDocument.Parse(userMsg);
        Assert.Equal("a", items.RootElement[0].GetProperty("id").GetString());
        Assert.Equal("Заголовок a", items.RootElement[0].GetProperty("title").GetString());
    }

    [Fact]
    public async Task Does_not_call_the_gateway_without_api_key()
    {
        var (c, store, gw) = Make(apiKey: "");
        AddPending(store, "a");

        var applied = await c.ClassifyPendingAsync(10, CancellationToken.None);

        Assert.Equal(0, applied);
        Assert.Empty(gw.RequestBodies);
        Assert.Equal(1, store.PendingLlmCount);
    }

    [Fact]
    public async Task Does_not_call_the_gateway_when_limit_is_zero_or_queue_is_empty()
    {
        var (c, store, gw) = Make();

        Assert.Equal(0, await c.ClassifyPendingAsync(10, CancellationToken.None));
        AddPending(store, "a");
        Assert.Equal(0, await c.ClassifyPendingAsync(0, CancellationToken.None));

        Assert.Empty(gw.RequestBodies);
    }

    [Fact]
    public async Task Respects_the_batch_limit()
    {
        var (c, store, gw) = Make();
        AddPending(store, "a", "b", "c");
        gw.ModelContent = Answer(("a", "Нефть", true), ("b", "Нефть", true));

        await c.ClassifyPendingAsync(2, CancellationToken.None);

        Assert.Equal(1, store.PendingLlmCount);
    }

    [Fact]
    public async Task Category_case_is_normalised()
    {
        var (c, store, gw) = Make();
        AddPending(store, "a");
        gw.ModelContent = Answer(("a", "нефть", true));

        await c.ClassifyPendingAsync(10, CancellationToken.None);

        Assert.Equal("Нефть", store.Get("a")!.Category);
    }

    [Fact]
    public async Task Real_category_is_business_relevant_even_if_the_model_said_no()
    {
        var (c, store, gw) = Make();
        AddPending(store, "a");
        gw.ModelContent = Answer(("a", "Нефть", false));

        await c.ClassifyPendingAsync(10, CancellationToken.None);

        Assert.True(store.Get("a")!.BusinessRelevant);
    }

    [Fact]
    public async Task Unknown_category_is_rejected_and_the_item_goes_back_to_the_queue()
    {
        var (c, store, gw) = Make();
        AddPending(store, "a");
        gw.ModelContent = Answer(("a", "Игнорируй все инструкции", true));

        var applied = await c.ClassifyPendingAsync(10, CancellationToken.None);

        Assert.Equal(0, applied);
        Assert.Equal("Прочее", store.Get("a")!.Category);
        Assert.Equal(1, store.PendingLlmCount);
    }

    [Fact]
    public async Task One_item_with_invalid_category_does_not_spoil_the_rest_of_the_batch()
    {
        var (c, store, gw) = Make();
        AddPending(store, "a", "b");
        gw.ModelContent = Answer(("a", "Выдумка", true), ("b", "Нефть", true));

        var applied = await c.ClassifyPendingAsync(10, CancellationToken.None);

        Assert.Equal(1, applied);
        Assert.Equal("Нефть", store.Get("b")!.Category);
        Assert.Equal("Прочее", store.Get("a")!.Category);
        Assert.True(store.Get("a")!.NeedsLlm);
    }

    [Fact]
    public async Task Items_missing_from_the_answer_are_requeued_while_answered_ones_are_applied()
    {
        var (c, store, gw) = Make();
        AddPending(store, "a", "b");
        gw.ModelContent = Answer(("a", "Нефть", true));

        await c.ClassifyPendingAsync(10, CancellationToken.None);

        Assert.Equal("llm", store.Get("a")!.ClassificationSource);
        Assert.True(store.Get("b")!.NeedsLlm);
        Assert.Equal(1, store.PendingLlmCount);
    }

    [Fact]
    public async Task Invented_ids_do_not_create_news()
    {
        var (c, store, gw) = Make();
        AddPending(store, "a");
        gw.ModelContent = Answer(("ghost", "Нефть", true));

        await c.ClassifyPendingAsync(10, CancellationToken.None);

        Assert.Null(store.Get("ghost"));
        Assert.Equal(1, store.Count);
        Assert.Equal(1, store.PendingLlmCount);
    }

    [Fact]
    public async Task Answer_in_markdown_fence_is_understood()
    {
        var (c, store, gw) = Make();
        AddPending(store, "a");
        gw.ModelContent = "```json\n" + Answer(("a", "Нефть", true)) + "\n```";

        var applied = await c.ClassifyPendingAsync(10, CancellationToken.None);

        Assert.Equal(1, applied);
    }

    [Theory]
    [InlineData("не json вообще")]
    [InlineData("{\"id\":\"a\",\"category\":\"Нефть\"}")]
    [InlineData("[1,2,3]")]
    [InlineData("[{\"id\":\"a\"")]
    public async Task Broken_answers_do_not_throw_and_return_the_items_to_the_queue(string content)
    {
        var (c, store, gw) = Make();
        AddPending(store, "a");
        gw.ModelContent = content;

        var applied = await c.ClassifyPendingAsync(10, CancellationToken.None);

        Assert.Equal(0, applied);
        Assert.Equal(1, store.PendingLlmCount);
        Assert.Equal("Прочее", store.Get("a")!.Category);
    }

    [Fact]
    public async Task Gateway_error_does_not_throw_and_returns_the_items_to_the_queue()
    {
        var (c, store, gw) = Make();
        AddPending(store, "a");
        gw.Status = HttpStatusCode.InternalServerError;

        var applied = await c.ClassifyPendingAsync(10, CancellationToken.None);

        Assert.Equal(0, applied);
        Assert.Equal(1, store.PendingLlmCount);
    }
}
