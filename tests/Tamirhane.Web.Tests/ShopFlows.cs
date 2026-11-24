using System.Net;
using System.Text.RegularExpressions;

namespace Tamirhane.Web.Tests;

/// <summary>Testlerde sÄ±k geÃ§en kullanÄ±cÄ± akÄ±ÅŸlarÄ±: atÃ¶lye sahibi olarak giriÅŸ, teknisyen daveti ve kabulÃ¼.</summary>
public sealed partial class ShopFlows(TamirhaneWebFactory app)
{
    public const string OwnerPassword = "kirmizi-kalem-42";
    public const string TechnicianPassword = "mavi-tornavida-17";

    /// <summary>AtÃ¶lye kaydÄ± + e-posta doÄŸrulama + giriÅŸ; sahibin tarayÄ±cÄ±sÄ±nÄ± dÃ¶ner.</summary>
    public async Task<Browser> OwnerAsync(string email)
    {
        var browser = app.NewBrowser();
        await browser.SubmitAsync("/account/register", "/account/register", new()
        {
            ["ShopName"] = "Bengkel Jaya", ["OwnerName"] = "Ayşe Kaya", ["Email"] = email, ["Password"] = OwnerPassword, ["Locale"] = "id",
        });
        await browser.GetAsync(LinkIn(email, "/account/confirm-email"));
        var login = await browser.SubmitAsync("/account/login", "/account/login", new() { ["Email"] = email, ["Password"] = OwnerPassword });
        Assert.Equal("/panel", Browser.RedirectOf(login));
        return browser;
    }

    public static Task<HttpResponseMessage> InviteAsync(Browser owner, string email) =>
        owner.SubmitAsync("/staff", "/staff/invite", new() { ["Invite.Email"] = email });

    public async Task<HttpResponseMessage> AcceptAsync(Browser browser, string email, string password = TechnicianPassword)
    {
        var link = InvitationLink(email);
        var token = link[(link.IndexOf("token=", StringComparison.Ordinal) + "token=".Length)..];
        return await browser.SubmitAsync(link, "/invitation", new()
        {
            ["Input.Token"] = Uri.UnescapeDataString(token),
            ["Input.DisplayName"] = "Mehmet Demir",
            ["Input.Password"] = password,
            ["Input.ConfirmPassword"] = password,
        });
    }

    /// <summary>Sahip davet eder, teknisyen kabul eder; teknisyenin tarayÄ±cÄ±sÄ±nÄ± dÃ¶ner.</summary>
    public async Task<Browser> TechnicianAsync(Browser owner, string email)
    {
        await InviteAsync(owner, email);
        var technician = app.NewBrowser();
        Assert.Equal("/panel", Browser.RedirectOf(await AcceptAsync(technician, email)));
        return technician;
    }

    /// <summary>Yeni mÃ¼ÅŸteriyle cihaz kabul eder; aÃ§Ä±lan iÅŸ emri sayfasÄ±nÄ±n yolunu dÃ¶ner.</summary>
    public static async Task<string> ReceiveAsync(Browser browser, string phone)
    {
        var response = await browser.SubmitAsync($"/work-orders/new?phone={Uri.EscapeDataString(phone)}", "/work-orders/new", new()
        {
            ["Input.Phone"] = phone, ["Input.CustomerName"] = "Zeynep Arslan", ["Input.KvkkConsent"] = "true",
            ["Input.DeviceKind"] = "Phone", ["Input.Brand"] = "Apple", ["Input.Model"] = "iPhone 13", ["Input.Complaint"] = "Ekran kÄ±rÄ±k",
        });
        return Browser.RedirectOf(response);
    }

    public string InvitationLink(string email) => LinkIn(email, "/invitation");

    /// <summary>Adrese giden, verilen sayfaya iÅŸaret eden son linkin yolu ve sorgusu.</summary>
    public string LinkIn(string email, string path)
    {
        var mails = app.Emails.SentTo(email);
        var mail = mails.LastOrDefault(m => (m.Text + "\n" + m.Html).Contains(path, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"No mail to {email} contains {path}. Got: {string.Join(" | ", mails.Select(m => m.Subject + " :: " + m.Text))}");
        var body = mail.Text.Contains(path, StringComparison.OrdinalIgnoreCase) ? mail.Text : WebUtility.HtmlDecode(mail.Html);
        return new Uri(LinkPattern().Matches(body).Last(m => m.Value.Contains(path, StringComparison.OrdinalIgnoreCase)).Value).PathAndQuery;
    }

    public static async Task<string> BodyAsync(Browser browser, string url) =>
        await (await browser.GetAsync(url)).Content.ReadAsStringAsync();

    [GeneratedRegex(@"https?://\S+")]
    private static partial Regex LinkPattern();
}
