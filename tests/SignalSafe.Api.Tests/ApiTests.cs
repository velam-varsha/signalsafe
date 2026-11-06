using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

using Microsoft.AspNetCore.Mvc.Testing;

using SignalSafe.Api.Contracts;
using SignalSafe.Api.Endpoints;

namespace SignalSafe.Api.Tests;

/// <summary>Runs the real API in memory and calls it over HTTP.</summary>
public sealed class ApiTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    private readonly HttpClient _client = factory.CreateClient();

    private static string Sample(string name) => Path.Combine(AppContext.BaseDirectory, "Samples", name);

    private static MultipartFormDataContent Upload(params (string Field, string Path)[] files)
    {
        var form = new MultipartFormDataContent();
        foreach (var (field, path) in files)
        {
            var content = new ByteArrayContent(File.ReadAllBytes(path));
            content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            form.Add(content, field, Path.GetFileName(path));
        }
        return form;
    }

    [Fact]
    public async Task Health_check_answers_ok()
    {
        var response = await _client.GetStringAsync("/health");

        response.Should().Contain("ok");
    }

    [Fact]
    public async Task Lists_the_two_samples()
    {
        var samples = await _client.GetFromJsonAsync<List<Sample>>("/api/v1/samples", Json);

        samples!.Select(s => s.Id).Should().Equal("westbrook", "kestrel");
    }

    [Fact]
    public async Task Checking_the_Westbrook_sample_returns_its_routes_diagram_and_findings()
    {
        var result = await _client.GetFromJsonAsync<CheckResult>("/api/v1/samples/westbrook/check", Json);

        result!.Layout.HasDiagram.Should().BeTrue();
        result.Layout.Sections.Should().OnlyContain(s => s.Line != null && s.Line.Count >= 2);
        result.Routes.Should().HaveCount(5);
        result.Routes.Single(r => r.Id == "S7(S)-S6").InTables.Should().BeFalse();
        result.Findings.Should().HaveCount(7);
        result.Tables!.RowCount.Should().Be(4);
        result.Rules.Should().HaveCount(10);
        result.Rules.Where(r => r.FindingCount == 0).Select(r => r.Id).Should().Equal("R02", "R06", "R08", "R09");
    }

    [Fact]
    public async Task Unknown_sample_is_not_found()
    {
        var response = await _client.GetAsync("/api/v1/samples/nowhere/check");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Uploaded_layout_and_tables_are_checked()
    {
        using var form = Upload(("layout", Sample("kestrel-lane-crossover.json")), ("tables", Sample("kestrel-control-tables.xlsx")));

        var response = await _client.PostAsync("/api/v1/check", form);
        var result = await response.Content.ReadFromJsonAsync<CheckResult>(Json);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        result!.Findings.Should().ContainSingle().Which.RuleId.Should().Be("R09");
        result.Conflicts.Count(c => c.Opposing).Should().Be(3);
    }

    [Fact]
    public async Task Layout_on_its_own_returns_routes_but_no_findings()
    {
        using var form = Upload(("layout", Sample("westbrook-junction.json")));

        var result = await (await _client.PostAsync("/api/v1/check", form)).Content.ReadFromJsonAsync<CheckResult>(Json);

        result!.Routes.Should().HaveCount(5);
        result.Tables.Should().BeNull();
        result.Findings.Should().BeEmpty();
    }

    [Fact]
    public async Task Wrong_file_type_gets_a_clear_message()
    {
        using var form = Upload(("layout", Sample("westbrook-control-tables.xlsx")));

        var response = await _client.PostAsync("/api/v1/check", form);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain(".json");
    }

    [Fact]
    public async Task Broken_json_gets_a_clear_message()
    {
        var path = Path.Combine(Path.GetTempPath(), $"broken-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(path, "{ \"sections\": [");
        try
        {
            using var form = Upload(("layout", path));
            var response = await _client.PostAsync("/api/v1/check", form);

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await response.Content.ReadAsStringAsync()).Should().Contain("not valid JSON");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task A_layout_with_problems_returns_them_instead_of_routes()
    {
        var path = Path.Combine(Path.GetTempPath(), $"bad-{Guid.NewGuid():N}.json");
        var json = (await File.ReadAllTextAsync(Sample("westbrook-junction.json"))).Replace("\"reverse\": \"T5\"", "\"reverse\": \"T3\"", StringComparison.Ordinal);
        await File.WriteAllTextAsync(path, json);
        try
        {
            using var form = Upload(("layout", path));
            var result = await (await _client.PostAsync("/api/v1/check", form)).Content.ReadFromJsonAsync<CheckResult>(Json);

            result!.LayoutIssues.Should().NotBeEmpty();
            result.Routes.Should().BeEmpty();
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData("/api/v1/templates/control-tables")]
    [InlineData("/api/v1/samples/westbrook/files/tables")]
    public async Task Excel_downloads_are_real_workbooks(string url)
    {
        var bytes = await _client.GetByteArrayAsync(url);

        bytes.Take(2).Should().Equal((byte)'P', (byte)'K'); // .xlsx files are zip archives
    }

    [Fact]
    public async Task Findings_report_is_an_Excel_download()
    {
        using var form = Upload(("layout", Sample("westbrook-junction.json")), ("tables", Sample("westbrook-control-tables.xlsx")));

        var response = await _client.PostAsync("/api/v1/check/report", form);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentDisposition!.FileName.Should().Contain("signalsafe-findings.xlsx");
    }

    [Fact]
    public async Task Draft_returns_control_tables_for_an_uploaded_layout()
    {
        using var form = Upload(("layout", Sample("westbrook-junction.json")));

        var response = await _client.PostAsync("/api/v1/draft", form);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Contain("spreadsheetml");
    }

    [Fact]
    public async Task OpenApi_document_lists_every_endpoint()
    {
        var doc = await _client.GetStringAsync("/openapi/v1.json");

        foreach (var path in new[] { "/api/v1/check", "/api/v1/check/report", "/api/v1/draft", "/api/v1/samples/{id}/check" })
            doc.Should().Contain(path);
    }
}
