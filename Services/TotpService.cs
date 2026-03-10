// Services/TotpService.cs
using OtpNet;
using QRCoder;

namespace ShopAPI.Services
{
    public class TotpService
    {
        private readonly IConfiguration _config;

        public TotpService(IConfiguration config)
        {
            _config = config;
        }

        // ── Generate a new secret key for the user ──
        public string GenerateSecretKey()
        {
            var key = KeyGeneration.GenerateRandomKey(20);
            // 20 random bytes → convert to Base32 string
            // Base32 = safe to display in QR codes
            return Base32Encoding.ToString(key);
        }

        // ── Verify the 6-digit code from Authenticator App ──
        public bool VerifyCode(string secretKey, string code)
        {
            try
            {
                var keyBytes = Base32Encoding.ToBytes(secretKey);
                var totp = new Totp(keyBytes);

                // VerificationWindow allows ±1 time step (30 sec tolerance)
                // Handles slight clock differences between phone and server
                return totp.VerifyTotp(
                    code.Trim(),
                    out _,
                    VerificationWindow.RfcSpecifiedNetworkDelay);
            }
            catch
            {
                return false; // invalid key format etc.
            }
        }

        // ── Generate QR code URI for Authenticator App ──
        public string GenerateQrCodeUri(string email, string secretKey)
        {
            var appName = _config["Jwt:Issuer"] ?? "ShopAPI";

            // Standard otpauth URI format that Google/Microsoft Authenticator reads
            return $"otpauth://totp/{Uri.EscapeDataString(appName)}" +
                   $":{Uri.EscapeDataString(email)}" +
                   $"?secret={secretKey}" +
                   $"&issuer={Uri.EscapeDataString(appName)}" +
                   $"&digits=6&period=30&algorithm=SHA1";
        }

        // ── Convert QR URI → Base64 PNG image ──
        public string GenerateQrCodeImage(string qrUri)
        {
            using var qrGenerator = new QRCodeGenerator();
            var qrData = qrGenerator.CreateQrCode(qrUri, QRCodeGenerator.ECCLevel.Q);
            var qrCode = new PngByteQRCode(qrData);
            var qrBytes = qrCode.GetGraphic(10); // 10 = pixel size per module
            return Convert.ToBase64String(qrBytes);
            // Returns: "iVBORw0KGgo..." → show as <img src="data:image/png;base64,...">
        }
    }
}