using FluentAssertions;
using TUnit.Core;
using TUnit.Core.Interfaces;

namespace Aero.Cms.E2E.Tests;

public sealed class EditorSmokeTests : IAsyncDisposable
{
    private readonly PlaywrightE2EFixture _fixture = new();

    [Before(Test)]
    public async Task SetupAsync() => await _fixture.InitializeAsync();

    [Test]
    public async Task LoginWorks()
    {
        await _fixture.LoginAsync();
        var page = _fixture.Page!;
        await page.GotoAsync($"{_fixture.BaseUrl}/manager/pages");
        page.Url.Should().Contain("/manager/").And.NotContain("/login");
    }

    public async ValueTask DisposeAsync() => await _fixture.DisposeAsync();
}