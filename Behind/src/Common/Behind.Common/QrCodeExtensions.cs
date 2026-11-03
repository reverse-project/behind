using QRCoder;
using static Behind.Common.FunctionalExtensions;

namespace Behind.Common;

public static class QrCodeExtensions
{
    extension(string content)
    {
        public Option<string> ToQrCodeSvg() =>
            Try(() => content.CreateQrCodeSvg())
                .Run()
                .Match<Option<string>>(_ => None, svg => Some(svg!));

        private string CreateQrCodeSvg()
        {
            using var generator = new QRCodeGenerator();
            using var data = generator.CreateQrCode(content, QRCodeGenerator.ECCLevel.M);

            return new SvgQRCode(data).GetGraphic(10);
        }
    }
}
