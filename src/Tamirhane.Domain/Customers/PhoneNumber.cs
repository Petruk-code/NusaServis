using Tamirhane.Domain.Common;

namespace Tamirhane.Domain.Customers;

/// <summary>
/// Nomor telepon dalam format E.164. Menerima format Indonesia (08..., +628..., 628..., 628)
/// dan menormalisasi ke +628... untuk penyimpanan/link WhatsApp.
/// </summary>
public sealed record PhoneNumber
{
    private PhoneNumber(string value) => Value = value;

    public string Value { get; }

    public static Result<PhoneNumber> Parse(string? input)
    {
        var text = input?.Trim() ?? "";
        if (text.Length == 0 || text.Length > 30)
            return CustomerErrors.InvalidPhone;

        var international = text.StartsWith('+');
        var digits = new string(text.Where(char.IsAsciiDigit).ToArray());
        if (digits.Length == 0)
            return CustomerErrors.InvalidPhone;
        // Hanya digit, spasi, tanda hubung, titik, kurung, dan +.
        if (text.Any(c => !char.IsAsciiDigit(c) && c is not (' ' or '-' or '.' or '(' or ')' or '+')))
            return CustomerErrors.InvalidPhone;

        if (!international && digits.StartsWith("00"))
        {
            international = true;
            digits = digits[2..];
        }

        if (!international)
        {
            if (digits.StartsWith('0'))
                digits = digits[1..];

            // Indonesia lokal: 8xxxxxxxxxx (mobile) or 2x/3x... (landline)
            if (digits.StartsWith('8'))
                digits = "62" + digits;
            else if (digits.Length is >= 9 and <= 12 && digits[0] is '2' or '3' or '4' or '5' or '6' or '7' or '9')
                digits = "62" + digits;
            // Turkey lokal lama (kompatibilitas data/test lama): 5xxxxxxxxx
            else if (digits.Length == 10 && digits[0] == '5')
                digits = "90" + digits;
            else if (digits.StartsWith("62"))
            {
                // already country code without +
            }
            else if (digits.Length == 10 && digits[0] != '1')
                digits = "62" + digits; // default lokal ke Indonesia
            else
                return CustomerErrors.InvalidPhone;
        }

        if (digits.StartsWith("62"))
        {
            var national = digits[2..];
            if (national.Length is < 8 or > 13 || national[0] == '0')
                return CustomerErrors.InvalidPhone;
        }
        else if (digits.StartsWith("90"))
        {
            var national = digits[2..];
            if (national.Length != 10 || national[0] is '0' or '1')
                return CustomerErrors.InvalidPhone;
        }
        else if (digits.Length is < 8 or > 15 || digits[0] == '0')
        {
            return CustomerErrors.InvalidPhone;
        }

        return new PhoneNumber("+" + digits);
    }

    /// <summary>Format 628... untuk wa.me.</summary>
    public string ToWhatsAppDigits() => Value.StartsWith('+') ? Value[1..] : Value;

    public override string ToString() => Value;
}
