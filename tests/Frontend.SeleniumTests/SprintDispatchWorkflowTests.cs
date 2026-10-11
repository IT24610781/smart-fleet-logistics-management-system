using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;
using Xunit;

namespace Frontend.SeleniumTests;

[Collection("Selenium collection")]
public class SprintDispatchWorkflowTests : Sprint3SeleniumTestBase
{
    private static readonly string Root = Environment.GetEnvironmentVariable("QA_OUTPUT_ROOT") ?? Path.Combine("TestResults", "QA-Sprint03", "browser", DateTime.UtcNow.ToString("yyyyMMdd-HHmmss"));
    private readonly List<object> Results = new();
    private readonly List<string> Failures = new();
    private readonly string Pw = "QaBrowser!" + Guid.NewGuid().ToString("N");
    private static readonly string AdminUsername = Environment.GetEnvironmentVariable("QA_ADMIN_USERNAME") ?? "admin";
    private static string AdminPassword => Environment.GetEnvironmentVariable("QA_ADMIN_PASSWORD") ?? throw new InvalidOperationException("Set QA_ADMIN_PASSWORD before running live QA.");
    private static readonly string AuthUrl = Environment.GetEnvironmentVariable("QA_AUTH_URL") ?? "http://localhost:5100";
    private static readonly string FleetUrl = Environment.GetEnvironmentVariable("QA_FLEET_URL") ?? "http://localhost:5041";
    private static readonly string TripUrl = Environment.GetEnvironmentVariable("QA_TRIP_URL") ?? "http://localhost:5051";
    private string dispatcherUsername = "", dispatcherToken = "", admin = "", driver = "", driverToken = "", driverId = "", other = "";

    private async Task<JsonNode> Api(string host, string path, string method = "GET", object? body = null, string? token = null)
    {
        using var c = new HttpClient
        {
            BaseAddress = new Uri(host)
        };
        if (token != null) c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var q = new HttpRequestMessage(new HttpMethod(method), path);
        if (body != null) q.Content = JsonContent.Create(body);
        var r = await c.SendAsync(q);
        var text = await r.Content.ReadAsStringAsync();
        r.EnsureSuccessStatusCode();
        return string.IsNullOrEmpty(text) ? new JsonObject() : JsonNode.Parse(text)!;
    }

    private async Task<string> Token(string name, string? pw = null) => (await Api(AuthUrl, "/api/auth/login", "POST", new
    {
        username = name,
        password = pw ?? Pw
    }))["token"]!.GetValue<string>();
    private object User(string name) => new
    {
        username = name,
        email = name + "@example.com",
        password = Pw,
        firstName = "QA",
        lastName = "Browser",
        contactNumber = "0771234567"
    };

    private void Go(string path)
    {
        Driver.Navigate().GoToUrl(FrontendBaseUrl + path);
    }
    private string Text() => Driver.FindElement(By.TagName("body")).Text;

    private void Fill(string id, string value)
    {
        var el = Wait.Until(d => d.FindElement(By.Id(id)));
        el.Clear();
        el.SendKeys(value);
    }

    private void Click(string text) => Wait.Until(d => (d.FindElements(By.CssSelector("[role=dialog]")).Any() ? d.FindElement(By.CssSelector("[role=dialog]")).FindElements(By.TagName("button")) : d.FindElements(By.TagName("button"))).FirstOrDefault(e => e.Displayed && e.Enabled && e.Text.Trim() == text))!.Click();

    private void Fresh()
    {
        Go("/login");
        ((IJavaScriptExecutor)Driver).ExecuteScript("localStorage.clear()");
        Go("/login");
    }

    private void Log(string name, string? pw = null)
    {
        Fresh();
        LoginAs(name, pw ?? Pw);
    }

    private async Task Case(int row, string name, Func<Task> action)
    {
        var status = "Pass";
        var detail = "Assertions passed";
        try
        {
            await action();
        }
        catch (Exception e)
        {
            status = "Fail";
            detail = e.GetType().Name + ": " + e.Message;
            Failures.Add($"Row {row} {name}: {detail}");
        }
        var file = $"ui-row{row}-{status}.png";
        ((ITakesScreenshot)Driver).GetScreenshot().SaveAsFile(Path.Combine(Root, "results", "ui-screenshots", file));
        Results.Add(new
        {
            sheet = "Selenium-UI Tests",
            row,
            name,
            status,
            detail,
            evidence = "results/ui-screenshots/" + file
        });
        File.WriteAllText(Path.Combine(Root, "results", "ui-results.json"), JsonSerializer.Serialize(Results, new JsonSerializerOptions
        {
            WriteIndented = true
        }));
    }

    private Task Sync(Action a)
    {
        a();
        return Task.CompletedTask;
    }

    private void RegisterFill(string name, string? email = null)
    {
        Fill("firstName", "QA");
        Fill("lastName", "Browser");
        Fill("username", name);
        Fill("email", email ?? name + "@example.com");
        Fill("password", Pw);
        Fill("confirmPassword", Pw);
        Fill("contactNumber", "0771234567");
    }

    private async Task<JsonNode> NewVehicle(int capacity)
    {
        return await Api(FleetUrl, "/api/vehicles", "POST", new
        {
            registrationNo = "QB" + Guid.NewGuid().ToString("N")[..10],
            type = "Van",
            capacity = capacity + " persons"
        }, admin);
    }

    private async Task<JsonNode> NewTrip(string label, int capacity = 987654) => await Api(TripUrl, "/api/trips", "POST", new
    {
        pickupLocation = label,
        dropoffLocation = "QA Browser Destination",
        requiredCapacity = capacity + " persons"
    }, dispatcherToken);

    private async Task Assign(JsonNode t, JsonNode v) => await Api(TripUrl, "/api/trips/" + t["id"] + "/assign", "POST", new
    {
        vehicleId = v["id"]!.GetValue<string>(),
        driverId
    }, dispatcherToken);
    private IWebElement VehicleAssign(string reg) => Wait.Until(d => d.FindElements(By.XPath("//div[contains(text(),'" + reg + "')]/ancestor::div[.//button[normalize-space()='Assign']][1]//button[normalize-space()='Assign']")).FirstOrDefault(e => e.Displayed));
    private IWebElement TripRow(string label) => Wait.Until(d => d.FindElements(By.CssSelector("tbody tr")).FirstOrDefault(e => e.Text.Contains(label)))!;

    private void Details(JsonNode t)
    {
        Go("/dispatcher/trips/" + t["id"] + "/details");
        Wait.Until(d => Text().Contains("Suggested vehicles"));
    }

    private void PickDriver()
    {
        new SelectElement(Wait.Until(d => d.FindElement(By.Id("driver-select")))).SelectByValue(driverId);
    }

    [Sprint3BrowserFact]
    [Trait("Category", "Sprint3Live")]
    public async Task WorkbookUiScenarios_MeetSprint3AcceptanceCriteria()
    {
        Directory.CreateDirectory(Path.Combine(Root, "results", "ui-screenshots"));
        admin = await Token(AdminUsername, AdminPassword);
        driver = "qabu" + Guid.NewGuid().ToString("N")[..8];
        var u = await Api(AuthUrl, "/api/users/drivers", "POST", User(driver), admin);
        driverId = u["id"]!.GetValue<string>();
        driverToken = await Token(driver);
        other = "qabo" + Guid.NewGuid().ToString("N")[..8];
        await Api(AuthUrl, "/api/users/drivers", "POST", User(other), admin);
        dispatcherUsername = "qab" + Guid.NewGuid().ToString("N")[..8];
        await Case(5, "Registration page", () => Sync(() =>
        {
            Fresh();
            Go("/register/dispatcher");
            Wait.Until(d => d.FindElement(By.Id("username")));
            foreach (var id in new[] {
 "firstName", "lastName", "username", "email", "password", "confirmPassword", "contactNumber" }) Assert.True(Driver.FindElement(By.Id(id)).Displayed);
        }));
        await Case(7, "Empty registration fields", () => Sync(() =>
        {
            Go("/register/dispatcher");
            Click("Create account");
            Wait.Until(d => d.FindElements(By.CssSelector("[aria-invalid='true']")).Count >= 6);
            Assert.Contains("/register/dispatcher", Driver.Url);
        }));
        await Case(8, "Invalid registration fields", () => Sync(() =>
        {
            Go("/register/dispatcher");
            RegisterFill("bad name");
            Fill("email", "bad");
            Fill("password", "short");
            Fill("confirmPassword", "short");
            Click("Create account");
            Wait.Until(d => d.FindElements(By.CssSelector("[aria-invalid='true']")).Count >= 3);
            Assert.Contains("/register/dispatcher", Driver.Url);
        }));
        await Case(6, "Valid browser registration", () => Sync(() =>
        {
            Go("/register/dispatcher");
            RegisterFill(dispatcherUsername);
            Click("Create account");
            Wait.Until(d => Text().Contains("Account created"));
            Wait.Until(d => d.Url.Contains("/login"));
        }));
        dispatcherToken = await Token(dispatcherUsername);
        await Case(10, "Fixed Dispatcher role", () => Sync(() =>
        {
            Go("/register/dispatcher");
            var role = Driver.FindElement(By.Id("role"));
            Assert.Equal("Dispatcher", role.GetAttribute("value"));
            Assert.NotNull(role.GetDomAttribute("readonly"));
            var claims = JsonNode.Parse(System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(dispatcherToken.Split('.')[1].Replace('-', '+').Replace('_', '/').PadRight((dispatcherToken.Split('.')[1].Length + 3) / 4 * 4, '='))))!;
            Assert.Contains("Dispatcher", claims.ToJsonString());
        }));
        await Case(9, "Duplicate username and email UI", () => Sync(() =>
        {
            Go("/register/dispatcher");
            RegisterFill(dispatcherUsername, "new" + Guid.NewGuid().ToString("N")[..8] + "@example.com");
            Click("Create account");
            Wait.Until(d => Text().Contains("Username already exists"));
            Assert.Contains("/register", Driver.Url);
            Go("/register/dispatcher");
            RegisterFill("new" + Guid.NewGuid().ToString("N")[..8], dispatcherUsername + "@example.com");
            Click("Create account");
            Wait.Until(d => Text().Contains("email already exists"));
            Assert.Contains("/register", Driver.Url);
        }));
        await Case(11, "Dispatcher login", () => Sync(() =>
        {
            Log(dispatcherUsername);
            Assert.Contains("/dispatcher/dashboard", Driver.Url);
        }));
        await Case(12, "Dispatcher login after logout", () => Sync(() =>
        {
            Go("/dispatcher/trips");
            Click("Logout");
            Wait.Until(d => d.Url.Contains("/login"));
            LoginAs(dispatcherUsername, Pw);
            Assert.Contains("/dispatcher/dashboard", Driver.Url);
        }));
        await Case(13, "Dispatcher Admin page denied", () => Sync(() =>
        {
            Go("/admin/drivers");
            Wait.Until(d => d.Url.Contains("/403") || d.Url.Contains("/dispatcher/dashboard"));
            Assert.DoesNotContain("/admin/drivers", Driver.Url);
        }));
        await Case(27, "Anonymous Dispatcher page denied", () => Sync(() =>
        {
            Fresh();
            Go("/dispatcher/trips");
            Wait.Until(d => d.Url.Contains("/login"));
        }));
        await Case(28, "Admin and Driver dashboards", () => Sync(() =>
        {
            Log(AdminUsername, AdminPassword);
            Assert.Contains("/admin/dashboard", Driver.Url);
            Log(driver);
            Assert.Contains("/driver/dashboard", Driver.Url);
        }));
        Log(dispatcherUsername);
        await Case(15, "Empty trip fields", () => Sync(() =>
        {
            Go("/dispatcher/trips/create");
            Click("Create trip");
            Wait.Until(d => Text().Contains("Pickup location is required") && Text().Contains("Drop-off location is required"));
            Assert.Contains("/create", Driver.Url);
        }));
        var best = await NewVehicle(987654);
        var alternate = await NewVehicle(987660);
        await Case(16, "Best match shown", () => Sync(() =>
        {
            Go("/dispatcher/trips/create");
            Fill("requiredCapacity", "987654 persons");
            Wait.Until(d => Text().Contains(best["registrationNo"]!.GetValue<string>()) && Text().Contains("Best fit"));
        }));
        await Case(17, "No eligible vehicle UI", () => Sync(() =>
        {
            Fill("requiredCapacity", "999999999 persons");
            Wait.Until(d => Text().Contains("No matching vehicles right now"));
        }));
        await Case(14, "Create trip browser", () => Sync(() =>
        {
            Go("/dispatcher/trips/create");
            var label = "QA UI create " + Guid.NewGuid().ToString("N")[..6];
            Fill("Pickup location-input", label);
            Fill("Drop-off location-input", "QA destination");
            Fill("requiredCapacity", "987654 persons");
            Click("Create trip");
            Wait.Until(d => d.Url.EndsWith("/dispatcher/trips"));
            Assert.Contains(label, TripRow(label).Text);
        }));
        var confirmed = await NewTrip("QA UI confirm " + Guid.NewGuid().ToString("N")[..6]);
        await Case(18, "Confirm best match UI", async () =>
        {
            Details(confirmed);
            PickDriver();
            VehicleAssign(best["registrationNo"]!.GetValue<string>()).Click();
            Click("Assign vehicle");
            Wait.Until(d => d.Url.EndsWith("/dispatcher/trips"));
            var saved = await Api(TripUrl, "/api/trips/" + confirmed["id"], token: dispatcherToken);
            Assert.Equal(best["id"]!.GetValue<string>(), saved["assignedVehicleId"]!.GetValue<string>());
            Assert.Contains("Assigned", TripRow(confirmed["pickupLocation"]!.GetValue<string>()).Text);
        });
        var altbest = await NewVehicle(987654);
        var overrideTrip = await NewTrip("QA UI override " + Guid.NewGuid().ToString("N")[..6]);
        await Case(19, "Override UI", async () =>
        {
            Details(overrideTrip);
            PickDriver();
            VehicleAssign(alternate["registrationNo"]!.GetValue<string>()).Click();
            Click("Assign vehicle");
            Wait.Until(d => d.Url.EndsWith("/dispatcher/trips"));
            var saved = await Api(TripUrl, "/api/trips/" + overrideTrip["id"], token: dispatcherToken);
            Assert.Equal(alternate["id"]!.GetValue<string>(), saved["assignedVehicleId"]!.GetValue<string>());
            Assert.Contains("Assigned", TripRow(overrideTrip["pickupLocation"]!.GetValue<string>()).Text);
        });
        var staleTrip = await NewTrip("QA UI stale " + Guid.NewGuid().ToString("N")[..6]);
        await Case(20, "Stale unavailable UI", async () =>
        {
            Details(staleTrip);
            PickDriver();
            VehicleAssign(altbest["registrationNo"]!.GetValue<string>()).Click();
            await Api(FleetUrl, "/api/vehicles/" + altbest["id"] + "/status", "PATCH", new
            {
                status = "On Trip"
            }, admin);
            Click("Assign vehicle");
            Wait.Until(d => d.FindElements(By.CssSelector("[role='alert']")).Any());
            var saved = await Api(TripUrl, "/api/trips/" + staleTrip["id"], token: dispatcherToken);
            Assert.Equal("Requested", saved["status"]!.GetValue<string>());
            Assert.Null(saved["assignedVehicleId"]);
        });
        await Case(21, "Driver accepts using UI", () => Sync(() =>
        {
            Log(driver);
            Go("/driver/trips");
            var row = TripRow(confirmed["pickupLocation"]!.GetValue<string>());
            row.FindElements(By.TagName("button")).Single(e => e.Text == "Accept").Click();
            Click("Accept");
            Wait.Until(d => TripRow(confirmed["pickupLocation"]!.GetValue<string>()).Text.Contains("In-Progress"));
        }));
        await Case(24, "Status and timestamp visible", () => Sync(() =>
        {
            Driver.Navigate().Refresh();
            var cells = TripRow(confirmed["pickupLocation"]!.GetValue<string>()).FindElements(By.TagName("td"));
            Assert.Equal("In-Progress", cells[4].Text);
            Assert.False(string.IsNullOrWhiteSpace(cells[5].Text));
            Assert.NotEqual("—", cells[5].Text);
        }));
        await Case(26, "Dispatcher sees Driver decision", () => Sync(() =>
        {
            Log(dispatcherUsername);
            Go("/dispatcher/trips");
            Assert.Contains("In-Progress", TripRow(confirmed["pickupLocation"]!.GetValue<string>()).Text);
            Driver.Navigate().Refresh();
            Assert.Contains("In-Progress", TripRow(confirmed["pickupLocation"]!.GetValue<string>()).Text);
        }));
        await Case(23, "Other Driver action unavailable", () => Sync(() =>
        {
            Log(other);
            Go("/driver/trips");
            Wait.Until(d => !Text().Contains("Loading"));
            Assert.DoesNotContain(overrideTrip["pickupLocation"]!.GetValue<string>(), Text());
            Assert.Empty(Driver.FindElements(By.XPath("//button[normalize-space()='Accept']")));
        }));
        await Case(22, "Driver rejects using UI", async () =>
        {
            Log(driver);
            Go("/driver/trips");
            TripRow(overrideTrip["pickupLocation"]!.GetValue<string>()).FindElements(By.TagName("button")).Single(e => e.Text == "Reject").Click();
            Click("Reject");
            Wait.Until(d => Text().Contains("Trip rejected and returned to Dispatch"));
            var saved = await Api(TripUrl, "/api/trips/" + overrideTrip["id"], token: dispatcherToken);
            Assert.Equal("Rejected", saved["status"]!.GetValue<string>());
        });
        var raceV = await NewVehicle(987654);
        var raceT = await NewTrip("QA UI race " + Guid.NewGuid().ToString("N")[..6]);
        await Assign(raceT, raceV);
        await Case(25, "Invalid stale lifecycle UI", async () =>
        {
            Log(driver);
            Go("/driver/trips");
            TripRow(raceT["pickupLocation"]!.GetValue<string>()).FindElements(By.TagName("button")).Single(e => e.Text == "Accept").Click();
            await Api(TripUrl, "/api/trips/" + raceT["id"] + "/respond", "POST", new
            {
                decision = "accept"
            }, driverToken);
            Click("Accept");
            Wait.Until(d => d.FindElements(By.CssSelector("[role='alert']")).Any(e => e.Text.Contains("In-Progress") || e.Text.Contains("already")));
            Driver.Navigate().Refresh();
            Assert.Contains("In-Progress", TripRow(raceT["pickupLocation"]!.GetValue<string>()).Text);
        });
        Assert.True(Failures.Count == 0, string.Join(Environment.NewLine, Failures));
    }
}
