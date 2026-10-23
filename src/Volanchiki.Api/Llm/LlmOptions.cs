namespace Volanchiki.Api.Llm;

public class LlmOptions
{
    public string ApiKey { get; set; } = "";
    public string BaseUrl { get; set; } = "https://changellenge.up.railway.app/v1";
    public string ModelLight { get; set; } = "google/gemma-4-26b-a4b-it";
    public string ModelReasoning { get; set; } = "z-ai/glm-5.3-flash";
    public string ModelDefault { get; set; } = "qwen/qwen3.6-35b-a3b";
    public string ModelResearch { get; set; } = "deepseek/deepseek-v4.1-flash";
}
