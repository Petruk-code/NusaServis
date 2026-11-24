using System.Globalization;
using Tamirhane.Application.Abstractions;
using Tamirhane.Infrastructure.Email;

namespace Tamirhane.Infrastructure.Identity;

/// <summary>Template email akun (ID/EN).</summary>
internal static class AccountEmails
{
    public static EmailMessage Confirmation(string to, string locale, Uri link) => locale == "en"
        ? EmailLayout.Build(to, "en", "Confirm your NusaServis account",
            ["Click the button below to confirm your email address. The link is valid for 24 hours."],
            new EmailAction("Confirm my email", link),
            "If you did not sign up for NusaServis, you can ignore this email.")
        : EmailLayout.Build(to, "id", "Konfirmasi akun NusaServis Anda",
            ["Klik tombol di bawah untuk mengonfirmasi alamat email Anda. Tautan berlaku selama 24 jam."],
            new EmailAction("Konfirmasi email saya", link),
            "Jika Anda tidak mendaftar di NusaServis, abaikan email ini.");

    public static EmailMessage AlreadyRegistered(string to, string locale, Uri loginLink, Uri resetLink) => locale == "en"
        ? EmailLayout.Build(to, "en", "You already have a NusaServis account",
            ["Someone tried to sign up with this email address, but it already has an account.",
             $"You can sign in here: {loginLink.AbsoluteUri}"],
            new EmailAction("Reset my password", resetLink),
            "If this wasn't you, you can ignore this email; your account has not changed.")
        : EmailLayout.Build(to, "id", "Email ini sudah terdaftar di NusaServis",
            ["Seseorang mencoba mendaftar dengan email ini, tetapi akun sudah ada.",
             $"Anda bisa masuk di sini: {loginLink.AbsoluteUri}"],
            new EmailAction("Atur ulang kata sandi", resetLink),
            "Jika ini bukan Anda, abaikan email ini; akun Anda tidak berubah.");

    public static EmailMessage PasswordReset(string to, string locale, Uri link) => locale == "en"
        ? EmailLayout.Build(to, "en", "Reset your NusaServis password",
            ["Click the button below to choose a new password. The link is valid for 1 hour and works only once."],
            new EmailAction("Choose a new password", link),
            "If you did not ask for this, you can ignore this email; your password has not changed.")
        : EmailLayout.Build(to, "id", "Atur ulang kata sandi NusaServis Anda",
            ["Klik tombol di bawah untuk membuat kata sandi baru. Tautan berlaku 1 jam dan hanya bisa dipakai sekali."],
            new EmailAction("Buat kata sandi baru", link),
            "Jika Anda tidak meminta ini, abaikan email ini; kata sandi Anda tidak berubah.");

    public static EmailMessage Invitation(StaffInvitationEmail invitation, Uri link)
    {
        var english = invitation.Locale == "en";
        var expires = invitation.ExpiresAt.ToOffset(TimeSpan.FromHours(7))
            .ToString("d MMMM yyyy HH:mm", CultureInfo.GetCultureInfo(english ? "en-GB" : "id-ID"))
            .Replace('.', ':');
        return english
            ? EmailLayout.Build(invitation.Email, "en", $"{invitation.ShopName} invited you to NusaServis",
                [$"{invitation.InvitedBy} invited you to join {invitation.ShopName} on NusaServis as a technician.",
                 $"Choose your name and password with the button below. The link works once and is valid until {expires} (WIB)."],
                new EmailAction("Accept the invitation", link),
                "If you don't know this shop, you can ignore this email; no account is created without the link.")
            : EmailLayout.Build(invitation.Email, "id", $"{invitation.ShopName} mengundang Anda ke NusaServis",
                [$"{invitation.InvitedBy} mengundang Anda bergabung ke {invitation.ShopName} di NusaServis sebagai teknisi.",
                 $"Atur nama dan kata sandi Anda lewat tombol di bawah. Tautan hanya berlaku sekali sampai {expires} (WIB)."],
                new EmailAction("Terima undangan", link),
                "Jika Anda tidak mengenal bengkel ini, abaikan email ini; akun tidak akan dibuat tanpa tautan ini.");
    }
}
