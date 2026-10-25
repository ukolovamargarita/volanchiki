using System.Xml;
using Volanchiki.Api.News;
using Xunit;

namespace Volanchiki.Tests;

public class RssNewsParserTests
{
    const string Rss = """
        <?xml version="1.0" encoding="UTF-8"?>
        <rss version="2.0">
          <channel>
            <title>Тестовая лента</title>
            <item>
              <title>ЦБ сохранил ключевую ставку 14%</title>
              <link>https://example.com/news/1</link>
              <guid>guid-1</guid>
              <description>&lt;p&gt;Банк России &lt;b&gt;сохранил&lt;/b&gt; ставку&amp;nbsp;14%&lt;/p&gt;</description>
              <pubDate>Fri, 11 Sep 2026 14:30:00 +0300</pubDate>
            </item>
            <item>
              <title></title>
              <link>https://example.com/news/2</link>
            </item>
            <item>
              <title>Новость без даты и со странной ссылкой</title>
              <link>not a url</link>
            </item>
          </channel>
        </rss>
        """;

    readonly RssNewsParser _parser = new();

    [Fact]
    public void Parses_rss_fields_and_strips_html()
    {
        var items = _parser.Parse(Rss, "Лента");
        var n = items[0];

        Assert.Equal("ЦБ сохранил ключевую ставку 14%", n.Title);
        Assert.Equal("Банк России сохранил ставку 14%", n.Summary);
        Assert.Equal("https://example.com/news/1", n.Url);
        Assert.Equal("Лента", n.Source);
        Assert.Equal("Ставка", n.Category);
        Assert.False(n.IsSeed);
        // 14:30 по Москве (+03:00) = 11:30 UTC
        Assert.Equal(new DateTimeOffset(2026, 9, 11, 11, 30, 0, TimeSpan.Zero), n.PublishedAt);
    }

    [Fact]
    public void Skips_items_without_title()
    {
        var items = _parser.Parse(Rss, "Лента");
        Assert.Equal(2, items.Count);
        Assert.False(items.Any(i => i.Url == "https://example.com/news/2"));
    }

    [Fact]
    public void Link_that_is_not_http_url_becomes_null()
    {
        var items = _parser.Parse(Rss, "Лента");
        Assert.Null(items[1].Url);
    }

    [Fact]
    public void Missing_date_falls_back_to_now()
    {
        var items = _parser.Parse(Rss, "Лента");
        var diff = Math.Abs((DateTimeOffset.UtcNow - items[1].PublishedAt).TotalSeconds);
        Assert.True(diff < 60);
    }

    [Fact]
    public void Id_is_stable_for_same_source_and_guid()
    {
        var a = _parser.Parse(Rss, "Лента")[0].Id;
        var b = _parser.Parse(Rss, "Лента")[0].Id;
        Assert.Equal(a, b);
        Assert.Equal(24, a.Length);
    }

    [Fact]
    public void Id_differs_between_sources()
    {
        var a = _parser.Parse(Rss, "Лента А")[0].Id;
        var b = _parser.Parse(Rss, "Лента Б")[0].Id;
        Assert.NotEqual(a, b);
    }

    [Fact]
    public void Long_summary_is_clipped_to_600_chars_plus_ellipsis()
    {
        var xml = "<rss><channel><item><title>Длинная</title><description>"
                  + new string('а', 1000)
                  + "</description></item></channel></rss>";
        var n = _parser.Parse(xml, "s")[0];

        Assert.Equal(603, n.Summary.Length);
        Assert.True(n.Summary.EndsWith("..."));
    }

    [Fact]
    public void Parses_atom_feed()
    {
        var xml = """
            <feed xmlns="http://www.w3.org/2005/Atom">
              <entry>
                <title>Инфляция замедлилась</title>
                <link href="https://example.com/a/1"/>
                <id>tag:example.com,2026:1</id>
                <summary>Росстат опубликовал данные</summary>
                <updated>2026-09-11T10:00:00Z</updated>
              </entry>
            </feed>
            """;

        var n = _parser.Parse(xml, "Atom")[0];

        Assert.Equal("Инфляция замедлилась", n.Title);
        Assert.Equal("https://example.com/a/1", n.Url);
        Assert.Equal("Инфляция", n.Category);
        Assert.Equal(new DateTimeOffset(2026, 9, 11, 10, 0, 0, TimeSpan.Zero), n.PublishedAt);
    }

    [Fact]
    public void Feed_without_items_gives_empty_list()
    {
        Assert.Empty(_parser.Parse("<rss><channel></channel></rss>", "s"));
    }

    [Fact]
    public void Broken_xml_throws_xml_exception()
    {
        Assert.Throws<XmlException>(() => _parser.Parse("<rss><channel>", "s"));
    }

    [Fact]
    public void Seed_fallback_has_unique_ids_and_is_marked_as_seed()
    {
        var seeds = RssNewsParser.BuildSeedFallback();

        Assert.True(seeds.Count >= 3);
        Assert.Equal(seeds.Count, seeds.Select(s => s.Id).Distinct().Count());
        Assert.False(seeds.Any(s => !s.IsSeed));
    }
}
