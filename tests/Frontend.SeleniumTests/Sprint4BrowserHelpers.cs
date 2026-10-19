using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;

namespace Frontend.SeleniumTests;

/// <summary>
/// Shared helpers for the Sprint 4 failure tests. They simulate a denied location, a fake location and an
/// unreachable service by injecting a small script into every page the browser opens next, the same
/// technique TC14-03 already uses. Run with QA_RUN_SPRINT3_UI=1 and QA_ADMIN_PASSWORD set.
/// </summary>
public abstract class Sprint4BrowserHelpers : Sprint4LiveBase
{
    private ChromeDriver Chrome => (ChromeDriver)Driver;

    protected bool BodyContains(string text) =>
        Driver.FindElement(By.TagName("body")).Text.Contains(text, StringComparison.OrdinalIgnoreCase);

    protected bool AlertIsShown() =>
        Driver.FindElements(By.CssSelector("[role='alert']")).Any(e => e.Displayed);

    private void RunOnEveryNewPage(string script) =>
        Chrome.ExecuteCdpCommand("Page.addScriptToEvaluateOnNewDocument",
            new Dictionary<string, object?> { ["source"] = script });

    /// <summary>The browser refuses the location request, as if the user had clicked Block.</summary>
    protected void DenyLocation() => RunOnEveryNewPage("""
        Object.defineProperty(Navigator.prototype, 'geolocation', { get: () => ({
            getCurrentPosition: (ok, fail) => fail && fail({ code: 1, message: 'User denied Geolocation' }),
            watchPosition: () => 0,
            clearWatch: () => {}
        }) });
        """);

    /// <summary>The browser reports a fixed position in Colombo.</summary>
    protected void FakeLocation() => RunOnEveryNewPage("""
        Object.defineProperty(Navigator.prototype, 'geolocation', { get: () => ({
            getCurrentPosition: (ok) => ok({ coords: { latitude: 6.9271, longitude: 79.8612, accuracy: 10 }, timestamp: Date.now() }),
            watchPosition: () => 0,
            clearWatch: () => {}
        }) });
        """);

    /// <summary>Any request whose URL contains the fragment fails, as if the service were down.</summary>
    protected void BlockRequests(string urlFragment) => RunOnEveryNewPage($$"""
        (() => {
            const fragment = '{{urlFragment}}';
            const open = XMLHttpRequest.prototype.open;
            XMLHttpRequest.prototype.open = function (method, url, ...rest) {
                this.__blocked = String(url).includes(fragment);
                return open.call(this, method, url, ...rest);
            };
            const send = XMLHttpRequest.prototype.send;
            XMLHttpRequest.prototype.send = function (body) {
                if (this.__blocked) { setTimeout(() => this.dispatchEvent(new ProgressEvent('error')), 0); return; }
                return send.call(this, body);
            };
            const realFetch = window.fetch;
            window.fetch = function (input, init) {
                const url = typeof input === 'string' ? input : (input && input.url) || '';
                if (url.includes(fragment)) return Promise.reject(new TypeError('Failed to fetch'));
                return realFetch.apply(this, arguments);
            };
        })();
        """);
}
