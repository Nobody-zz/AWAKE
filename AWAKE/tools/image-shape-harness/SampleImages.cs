// 由 tools/image-shape-harness/make_sample_images.py 产出，样本来自 Pillow（与本仓储的解析代码无关）。
// 真尺寸（Pillow 自报）写进常量，供 C# 侧当独立判据；不要手改，改生成器后重跑。
using System;

namespace Awake.ImageShapeHarness;

internal static class SampleImages
{
    internal const string PngFileFormat = "PNG";
    internal const int PngWidth = 320;
    internal const int PngHeight = 192;
    internal const string PngBase64 =
        "iVBORw0KGgoAAAANSUhEUgAAAUAAAADACAIAAAD6NS2RAAAE90lEQVR4nO3ViQ0CARDDQHNA/y1TRoR2pFTgfK/q3UMIIPD+wyJ8" +
        "+j5FCCDw/GMRFHjvASGQAgsBAt1bQw+894AQSIGFAIHuraEH3ntACKTAQoBA99bQA+89IARSYCFAoHtr6IH3HhACKbAQINC9NfTA" +
        "ew8IgRRYCBDo3hp64L0HhEAKLAQIdG8NPfDeA0IgBRYCBLq3hh547wEhkAILAQLdW0MPvPeAEEiBhQCB7q2hB957QAikwEKAQPfW" +
        "0APvPSAEUmAhQKB7a+iB9x4QAimwECDQvTX0wHsPCIEUWAgQ6N4aeuC9B4RACiwECHRvDT3w3gNCIAUWAgS6t4YeeO8BIZACCwEC" +
        "3VtDD7z3gBBIgYUAge6toQfee0AIpMBCgED31tAD7z0gBFJgIUCge2vogfceEAIpsBAg0L019MB7DwiBFFgIEOjeGnrgvQeEQAos" +
        "BAh0bw098N4DQiAFFgIEureGHnjvASGQAgsBAt1bQw+894AQSIGFAIHuraEH3ntACKTAQoBA99bQA+89IARSYCFAoHtr6IH3HhAC" +
        "KbAQINC9NfTAew8IgRRYCBDo3hp64L0HhEAKLAQIdG8NPfDeA0IgBRYCBLq3hh547wEhkAILAQLdW0MPvPeAEEiBhQCB7q2hB957" +
        "QAikwEKAQPfW0APvPSAEUmAhQKB7a+iB9x4QAimwECDQvTX0wHsPCIEUWAgQ6N4aeuC9B4RACiwECHRvDT3w3gNCIAUWAgS6t4Ye" +
        "eO8BIZACCwEC3VtDD7z3gBBIgYUAge6toQfee0AIpMBCgED31tAD7z0gBFJgIUCge2vogfceEAIpsBAg0L019MB7DwiBFFgIEOje" +
        "GnrgvQeEQAosBAh0bw098N4DQiAFFgIEureGHnjvASGQAgsBAt1bQw+894AQSIGFAIHuraEH3ntACKTAQoBA99bQA+89IARSYCFA" +
        "oHtr6IH3HhACKbAQINC9NfTAew8IgRRYCBDo3hp64L0HhEAKLAQIdG8NPfDeA0IgBRYCBLq3hh547wEhkAILAQLdW0MPvPeAEEiB" +
        "hQCB7q2hB957QAikwEKAQPfW0APvPSAEUmAhQKB7a+iB9x4QAimwECDQvTX0wHsPCIEUWAgQ6N4aeuC9B4RACiwECHRvDT3w3gNC" +
        "IAUWAgS6t4YeeO8BIZACCwEC3VtDD7z3gBBIgYUAge6toQfee0AIpMBCgED31tAD7z0gBFJgIUCge2vogfceEAIpsBAg0L019MB7" +
        "DwiBFFgIEOjeGnrgvQeEQAosBAh0bw098N4DQiAFFgIEureGHnjvASGQAgsBAt1bQw+894AQSIGFAIHuraEH3ntACKTAQoBA99bQ" +
        "A+89IARSYCFAoHtr6IH3HhACKbAQINC9NfTAew8IgRRYCBDo3hp64L0HhEAKLAQIdG8NPfDeA0IgBRYCBLq3hh547wEhkAILAQLd" +
        "W0MPvPeAEEiBhQCB7q2hB957QAikwEKAQPfW0APvPSAEUmAhQKB7a+iB9x4QAimwECDQvTX0wHsPCIEUWAgQ6N4aeuC9B4RACiwE" +
        "CHRvDT3w3gNCIAUWAgS6t4YeeO8BIZACCwEC3VtDD7z3gBBIgYUAge6toQfee0AIpMBCgED31tAD7z0gBFJgIUCge2vogfceEAIp" +
        "sBAg0L019MB7DwiBFFgIEOjeGnrgvQeEQAosBAh0bw1/EeXqgx0sqfUAAAAASUVORK5CYII=";

    internal const string JpegFileFormat = "JPEG";
    internal const int JpegWidth = 320;
    internal const int JpegHeight = 192;
    internal const string JpegBase64 =
        "/9j/4AAQSkZJRgABAQAAAQABAAD/2wBDAAYEBAUEBAYFBQUGBgYHCQ4JCQgICRINDQoOFRIWFhUSFBQXGiEcFxgfGRQUHScdHyIj" +
        "JSUlFhwpLCgkKyEkJST/2wBDAQYGBgkICREJCREkGBQYJCQkJCQkJCQkJCQkJCQkJCQkJCQkJCQkJCQkJCQkJCQkJCQkJCQkJCQk" +
        "JCQkJCQkJCT/wAARCADAAUADASIAAhEBAxEB/8QAHwAAAQUBAQEBAQEAAAAAAAAAAAECAwQFBgcICQoL/8QAtRAAAgEDAwIEAwUF" +
        "BAQAAAF9AQIDAAQRBRIhMUEGE1FhByJxFDKBkaEII0KxwRVS0fAkM2JyggkKFhcYGRolJicoKSo0NTY3ODk6Q0RFRkdISUpTVFVW" +
        "V1hZWmNkZWZnaGlqc3R1dnd4eXqDhIWGh4iJipKTlJWWl5iZmqKjpKWmp6ipqrKztLW2t7i5usLDxMXGx8jJytLT1NXW19jZ2uHi" +
        "4+Tl5ufo6erx8vP09fb3+Pn6/8QAHwEAAwEBAQEBAQEBAQAAAAAAAAECAwQFBgcICQoL/8QAtREAAgECBAQDBAcFBAQAAQJ3AAEC" +
        "AxEEBSExBhJBUQdhcRMiMoEIFEKRobHBCSMzUvAVYnLRChYkNOEl8RcYGRomJygpKjU2Nzg5OkNERUZHSElKU1RVVldYWVpjZGVm" +
        "Z2hpanN0dXZ3eHl6goOEhYaHiImKkpOUlZaXmJmaoqOkpaanqKmqsrO0tba3uLm6wsPExcbHyMnK0tPU1dbX2Nna4uPk5ebn6Onq" +
        "8vP09fb3+Pn6/9oADAMBAAIRAxEAPwD5oisfarkVj7VrRWPtVyKx9qmFQ5sPjjJisfarkVj7VrRWPtVyKx9q64VD3MPjvMyYrH2q" +
        "5FY+1a0Vj7VcisfauqFQ93D44yYrH2q5FY+1a0Vj7VcisfauuFQ9zD47zMmKx9quRWPtWtFY+1XIrH2rrhUPdw+O8zkPFlj/AMU5" +
        "d8f3P/Q1rz6Kx9q9j8WWP/FOXfH9z/0Na8+isfavrskqfuX6/oj8x4/x3/CnT/69r/0qRkxWPtVyKx9q1orH2q5FY+1fQQqHzeHx" +
        "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" +
        "8d5n/9k=";

    internal const string GifFileFormat = "GIF";
    internal const int GifWidth = 24;
    internal const int GifHeight = 14;
    internal const string GifBase64 =
        "R0lGODdhGAAOAIcAAERBRz9BRDxBQkU8RkI8RD88Qjw8QDhBPzNBPDg8PTM8OkQ3Qz83QDw3Pjg3OzM3OC9BOSpBNidBNC88Nyo8" +
        "NCc8MiRBMiFBMCQ8MCE8Li83NSo3Mic3MCM3LUQyQT8yPjwyPEUtQEItPj8tPDwtOjgyOTMyNjgtNzMtNC8yMyoyMCcyLjAtMi0t" +
        "MCotLictLCQyLCEyKiQtKiEtKEQoPT8oOjwoOEQjOz8jODwjNjgoNTMoMjkjNDYjMjMjMEQeOT8eNjweNDgeMTMeLi8oLyooLCco" +
        "Ki8jLSojKicjKCQoKCEoJiQjJiEjJDAeLC0eKioeKCceJiMeIx1BLRhBKhVBKB48LBs8Khg8KBU8JhFBJRE8Ix03KRg3JhU3JBI3" +
        "Ig83IAtBIQZBHgs8HwY8HAJBGwM8GgA8GAs3HQY3GgM3GAA3Fh0yJxgyJBUyIh0tJRgtIhUtIBIyIA8yHhItHg8tHAsyGwYyGAst" +
        "GQYtFgIyFQMtFAAtEh0oIxgoIBUoHh0jIRgjHhUjHBEoGxIjGg8jGB0eHxgeHBUeGhEeFwsoFwYoFAsjFQYjEgIoEQIjDwseEwYe" +
        "EAMeDgAeDEQZNz8ZNDwZMkQUNT8UMjwUMDgZLzMZLDgULTMUKkQPMz8PMDwPLjkPLDYPKjMPKEQKMT8KLjwKLDgKKTMKJi8ZKSoZ" +
        "JicZJC8UJyoUJCcUIiQZIiEZICMUHy8PJSoPIicPICQPHiEPHC8KIyoKICcKHiMKG0QFLz8FLDwFKkQALT8AKjwAKDgFJzMFJDkA" +
        "JjYAJDMAIi8FISoFHicFHDAAIC0AHioAHCcAGiMFGSQAGCEAFh0ZHRgZGhUZGB0UGxgUGBUUFhEZFRIUFA8UEh0PGRgPFhUPFBEP" +
        "ER0KFxgKFBUKEhEKDwsZEQYZDgsUDwYUDAMZDAAZCgIUCQsPDQYPCgMPCAAPBgsKCwYKCAIKBR0FFRgFEhUFEB0AExgAEBUADhIF" +
        "Dg8FDBIADA8ACgsFCQYFBgsABwYABAIFAwMAAgAAACwAAAAAGAAOAAAI/wD/+eO3bx++e/TmyZP3zBmzZcqSFSM2DNgvX7769dOX" +
        "L5+9evHgvXvXrBmyY8aMCQsWrBevXbvcuWvHjh04cN+8deumS1cuXLduoTp1ylQpUqTWqUuHDh03btu0ZctmqxatWbJkjRIVCtQn" +
        "T57OnSM3bhy2a9WoTZsWK5arVqxYdeLEKROmS5fMlRMXLpw1a9KiQYMG69UqValSbdKkyVIlSpQmSYoECVKiRIgOGTIkRUoUKE+c" +
        "DBEiJAiQHz8ePWrEiFEhQoICAQLUhEkSJEeO+OjBIweOGzccOVqkSNGgQX/89OmzRImRIkSI7NChw0YNGjT47MmDB08dOnHgvIh5" +
        "M0PGCxctWKA4cYLECBEh9Oi5Y8fOHDlu2rBhEwPGChUppGBCCSWA8IEHHqyhRhpooAHGF150wQUXHXTAwQYaaPCAAw40wMACC5xh" +
        "BhljjLHFFllgcYUVGWBQAQUTTKBAAgkYUAABA5RRhhhhhKGFFlVQMcUUF1ggQQQQQIDAAQcIEAAAAAQEADs=";

    internal const string BmpFileFormat = "BMP";
    internal const int BmpWidth = 64;
    internal const int BmpHeight = 32;
    internal const string BmpBase64 =
        "Qk02GAAAAAAAADYAAAAoAAAAQAAAACAAAAABABgAAAAAAAAYAADEDgAAxA4AAAAAAAAAAAAAPpsAQJsDQpsGRJsJRpsMSJsPSpsS" +
        "TJsVTpsYUJsbUpseVJshVpskWJsnWpsqXJstXpswYJszYps2ZJs5Zps8aJs/aptCbJtFbptIcJtLcptOdJtRdptUeJtXeptafJtd" +
        "fptggJtjgptmhJtphptsiJtviptyjJt1jpt4kJt7kpt+lJuBlpuEmJuHmpuKnJuNnpuQoJuTopuWpJuZppucqJufqpuirJulrpuo" +
        "sJurspuutJuxtpu0uJu3upu6vJu9PJYAPpYDQJYGQpYJRJYMRpYPSJYSSpYVTJYYTpYbUJYeUpYhVJYkVpYnWJYqWpYtXJYwXpYz" +
        "YJY2YpY5ZJY8ZpY/aJZCapZFbJZIbpZLcJZOcpZRdJZUdpZXeJZaepZdfJZgfpZjgJZmgpZphJZshpZviJZyipZ1jJZ4jpZ7kJZ+" +
        "kpaBlJaElpaHmJaKmpaNnJaQnpaToJaWopaZpJacppafqJaiqpalrJaorparsJauspaxtJa0tpa3uJa6upa9OpEAPJEDPpEGQJEJ" +
        "QpEMRJEPRpESSJEVSpEYTJEbTpEeUJEhUpEkVJEnVpEqWJEtWpEwXJEzXpE2YJE5YpE8ZJE/ZpFCaJFFapFIbJFLbpFOcJFRcpFU" +
        "dJFXdpFaeJFdepFgfJFjfpFmgJFpgpFshJFvhpFyiJF1ipF4jJF7jpF+kJGBkpGElJGHlpGKmJGNmpGQnJGTnpGWoJGZopGcpJGf" +
        "ppGiqJGlqpGorJGrrpGusJGxspG0tJG3tpG6uJG9OIwAOowDPIwGPowJQIwMQowPRIwSRowVSIwYSowbTIweTowhUIwkUownVIwq" +
        "VowtWIwwWowzXIw2Xow5YIw8Yow/ZIxCZoxFaIxIaoxLbIxOboxRcIxUcoxXdIxadoxdeIxgeoxjfIxmfoxpgIxsgoxvhIxyhox1" +
        "iIx4iox7jIx+joyBkIyEkoyHlIyKloyNmIyQmoyTnIyWnoyZoIycooyfpIyipoylqIyoqoyrrIyuroyxsIy0soy3tIy6toy9NocA" +
        "OIcDOocGPIcJPocMQIcPQocSRIcVRocYSIcbSoceTIchTockUIcnUocqVIctVocwWIczWoc2XIc5Xoc8YIc/YodCZIdFZodIaIdL" +
        "aodObIdRbodUcIdXcodadIdddodgeIdjeodmfIdpfodsgIdvgodyhId1hod4iId7iod+jIeBjoeEkIeHkoeKlIeNloeQmIeTmoeW" +
        "nIeZnoecoIefooeipIelpoeoqIerqoeurIexroe0sIe3soe6tIe9NIIANoIDOIIGOoIJPIIMPoIPQIISQoIVRIIYRoIbSIIeSoIh" +
        "TIIkToInUIIqUoItVIIwVoIzWII2WoI5XII8XoI/YIJCYoJFZIJIZoJLaIJOaoJRbIJUboJXcIJacoJddIJgdoJjeIJmeoJpfIJs" +
        "foJvgIJygoJ1hIJ4hoJ7iIJ+ioKBjIKEjoKHkIKKkoKNlIKQloKTmIKWmoKZnIKcnoKfoIKiooKlpIKopoKrqIKuqoKxrIK0roK3" +
        "sIK6soK9Mn0ANH0DNn0GOH0JOn0MPH0PPn0SQH0VQn0YRH0bRn0eSH0hSn0kTH0nTn0qUH0tUn0wVH0zVn02WH05Wn08XH0/Xn1C" +
        "YH1FYn1IZH1LZn1OaH1Ran1UbH1Xbn1acH1dcn1gdH1jdn1meH1pen1sfH1vfn1ygH11gn14hH17hn1+iH2Bin2EjH2Hjn2KkH2N" +
        "kn2QlH2Tln2WmH2Zmn2cnH2fnn2ioH2lon2opH2rpn2uqH2xqn20rH23rn26sH29MHgAMngDNHgGNngJOHgMOngPPHgSPngVQHgY" +
        "QngbRHgeRnghSHgkSngnTHgqTngtUHgwUngzVHg2Vng5WHg8Wng/XHhCXnhFYHhIYnhLZHhOZnhRaHhUanhXbHhabnhdcHhgcnhj" +
        "dHhmdnhpeHhsenhvfHhyfnh1gHh4gnh7hHh+hniBiHiEiniHjHiKjniNkHiQkniTlHiWlniZmHicmnifnHiinniloHioonirpHiu" +
        "pnixqHi0qni3rHi6rni9LnMAMHMDMnMGNHMJNnMMOHMPOnMSPHMVPnMYQHMbQnMeRHMhRnMkSHMnSnMqTHMtTnMwUHMzUnM2VHM5" +
        "VnM8WHM/WnNCXHNFXnNIYHNLYnNOZHNRZnNUaHNXanNabHNdbnNgcHNjcnNmdHNpdnNseHNvenNyfHN1fnN4gHN7gnN+hHOBhnOE" +
        "iHOHinOKjHONjnOQkHOTknOWlHOZlnOcmHOfmnOinHOlnnOooHOronOupHOxpnO0qHO3qnO6rHO9LG4ALm4DMG4GMm4JNG4MNm4P" +
        "OG4SOm4VPG4YPm4bQG4eQm4hRG4kRm4nSG4qSm4tTG4wTm4zUG42Um45VG48Vm4/WG5CWm5FXG5IXm5LYG5OYm5RZG5UZm5XaG5a" +
        "am5dbG5gbm5jcG5mcm5pdG5sdm5veG5yem51fG54fm57gG5+gm6BhG6Ehm6HiG6Kim6NjG6Qjm6TkG6Wkm6ZlG6clm6fmG6imm6l" +
        "nG6onm6roG6uom6xpG60pm63qG66qm69KmkALGkDLmkGMGkJMmkMNGkPNmkSOGkVOmkYPGkbPmkeQGkhQmkkRGknRmkqSGktSmkw" +
        "TGkzTmk2UGk5Umk8VGk/VmlCWGlFWmlIXGlLXmlOYGlRYmlUZGlXZmlaaGldamlgbGljbmlmcGlpcmlsdGlvdmlyeGl1eml4fGl7" +
        "fml+gGmBgmmEhGmHhmmKiGmNimmQjGmTjmmWkGmZkmmclGmflmmimGmlmmmonGmrnmmuoGmxomm0pGm3pmm6qGm9KGQAKmQDLGQG" +
        "LmQJMGQMMmQPNGQSNmQVOGQYOmQbPGQePmQhQGQkQmQnRGQqRmQtSGQwSmQzTGQ2TmQ5UGQ8UmQ/VGRCVmRFWGRIWmRLXGROXmRR" +
        "YGRUYmRXZGRaZmRdaGRgamRjbGRmbmRpcGRscmRvdGRydmR1eGR4emR7fGR+fmSBgGSEgmSHhGSKhmSNiGSQimSTjGSWjmSZkGSc" +
        "kmSflGSilmSlmGSommSrnGSunmSxoGS0omS3pGS6pmS9Jl8AKF8DKl8GLF8JLl8MMF8PMl8SNF8VNl8YOF8bOl8ePF8hPl8kQF8n" +
        "Ql8qRF8tRl8wSF8zSl82TF85Tl88UF8/Ul9CVF9FVl9IWF9LWl9OXF9RXl9UYF9XYl9aZF9dZl9gaF9jal9mbF9pbl9scF9vcl9y" +
        "dF91dl94eF97el9+fF+Bfl+EgF+Hgl+KhF+Nhl+QiF+Til+WjF+Zjl+ckF+fkl+ilF+lll+omF+rml+unF+xnl+0oF+3ol+6pF+9" +
        "JFoAJloDKFoGKloJLFoMLloPMFoSMloVNFoYNlobOFoeOlohPFokPlonQFoqQlotRFowRlozSFo2Slo5TFo8Tlo/UFpCUlpFVFpI" +
        "VlpLWFpOWlpRXFpUXlpXYFpaYlpdZFpgZlpjaFpmalppbFpsblpvcFpyclp1dFp4dlp7eFp+elqBfFqEflqHgFqKglqNhFqQhlqT" +
        "iFqWilqZjFqcjlqfkFqiklqllFqollqrmFqumlqxnFq0nlq3oFq6olq9IlUAJFUDJlUGKFUJKlUMLFUPLlUSMFUVMlUYNFUbNlUe" +
        "OFUhOlUkPFUnPlUqQFUtQlUwRFUzRlU2SFU5SlU8TFU/TlVCUFVFUlVIVFVLVlVOWFVRWlVUXFVXXlVaYFVdYlVgZFVjZlVmaFVp" +
        "alVsbFVvblVycFV1clV4dFV7dlV+eFWBelWEfFWHflWKgFWNglWQhFWThlWWiFWZilWcjFWfjlWikFWlklWolFWrllWumFWxmlW0" +
        "nFW3nlW6oFW9IFAAIlADJFAGJlAJKFAMKlAPLFASLlAVMFAYMlAbNFAeNlAhOFAkOlAnPFAqPlAtQFAwQlAzRFA2RlA5SFA8SlA/" +
        "TFBCTlBFUFBIUlBLVFBOVlBRWFBUWlBXXFBaXlBdYFBgYlBjZFBmZlBpaFBsalBvbFByblB1cFB4clB7dFB+dlCBeFCEelCHfFCK" +
        "flCNgFCQglCThFCWhlCZiFCcilCfjFCijlClkFCoklCrlFCullCxmFC0mlC3nFC6nlC9HksAIEsDIksGJEsJJksMKEsPKksSLEsV" +
        "LksYMEsbMkseNEshNkskOEsnOksqPEstPkswQEszQks2REs5Rks8SEs/SktCTEtFTktIUEtLUktOVEtRVktUWEtXWktaXEtdXktg" +
        "YEtjYktmZEtpZktsaEtvaktybEt1bkt4cEt7ckt+dEuBdkuEeEuHekuKfEuNfkuQgEuTgkuWhEuZhkuciEufikuijEuljkuokEur" +
        "kkuulEuxlku0mEu3mku6nEu9HEYAHkYDIEYGIkYJJEYMJkYPKEYSKkYVLEYYLkYbMEYeMkYhNEYkNkYnOEYqOkYtPEYwPkYzQEY2" +
        "QkY5REY8RkY/SEZCSkZFTEZITkZLUEZOUkZRVEZUVkZXWEZaWkZdXEZgXkZjYEZmYkZpZEZsZkZvaEZyakZ1bEZ4bkZ7cEZ+ckaB" +
        "dEaEdkaHeEaKekaNfEaQfkaTgEaWgkaZhEachkafiEaiikaljEaojkarkEaukkaxlEa0lka3mEa6mka9GkEAHEEDHkEGIEEJIkEM" +
        "JEEPJkESKEEVKkEYLEEbLkEeMEEhMkEkNEEnNkEqOEEtOkEwPEEzPkE2QEE5QkE8REE/RkFCSEFFSkFITEFLTkFOUEFRUkFUVEFX" +
        "VkFaWEFdWkFgXEFjXkFmYEFpYkFsZEFvZkFyaEF1akF4bEF7bkF+cEGBckGEdEGHdkGKeEGNekGQfEGTfkGWgEGZgkGchEGfhkGi" +
        "iEGlikGojEGrjkGukEGxkkG0lEG3lkG6mEG9GDwAGjwDHDwGHjwJIDwMIjwPJDwSJjwVKDwYKjwbLDweLjwhMDwkMjwnNDwqNjwt" +
        "ODwwOjwzPDw2Pjw5QDw8Qjw/RDxCRjxFSDxISjxLTDxOTjxRUDxUUjxXVDxaVjxdWDxgWjxjXDxmXjxpYDxsYjxvZDxyZjx1aDx4" +
        "ajx7bDx+bjyBcDyEcjyHdDyKdjyNeDyQejyTfDyWfjyZgDycgjyfhDyihjyliDyoijyrjDyujjyxkDy0kjy3lDy6ljy9FjcAGDcD" +
        "GjcGHDcJHjcMIDcPIjcSJDcVJjcYKDcbKjceLDchLjckMDcnMjcqNDctNjcwODczOjc2PDc5Pjc8QDc/QjdCRDdFRjdISDdLSjdO" +
        "TDdRTjdUUDdXUjdaVDddVjdgWDdjWjdmXDdpXjdsYDdvYjdyZDd1Zjd4aDd7ajd+bDeBbjeEcDeHcjeKdDeNdjeQeDeTejeWfDeZ" +
        "fjecgDefgjeihDelhjeoiDerijeujDexjje0kDe3kje6lDe9FDIAFjIDGDIGGjIJHDIMHjIPIDISIjIVJDIYJjIbKDIeKjIhLDIk" +
        "LjInMDIqMjItNDIwNjIzODI2OjI5PDI8PjI/QDJCQjJFRDJIRjJLSDJOSjJRTDJUTjJXUDJaUjJdVDJgVjJjWDJmWjJpXDJsXjJv" +
        "YDJyYjJ1ZDJ4ZjJ7aDJ+ajKBbDKEbjKHcDKKcjKNdDKQdjKTeDKWejKZfDKcfjKfgDKigjKlhDKohjKriDKuijKxjDK0jjK3kDK6" +
        "kjK9Ei0AFC0DFi0GGC0JGi0MHC0PHi0SIC0VIi0YJC0bJi0eKC0hKi0kLC0nLi0qMC0tMi0wNC0zNi02OC05Oi08PC0/Pi1CQC1F" +
        "Qi1IRC1LRi1OSC1RSi1UTC1XTi1aUC1dUi1gVC1jVi1mWC1pWi1sXC1vXi1yYC11Yi14ZC17Zi1+aC2Bai2EbC2Hbi2KcC2Nci2Q" +
        "dC2Tdi2WeC2Zei2cfC2ffi2igC2lgi2ohC2rhi2uiC2xii20jC23ji26kC29ECgAEigDFCgGFigJGCgMGigPHCgSHigVICgYIigb" +
        "JCgeJighKCgkKignLCgqLigtMCgwMigzNCg2Nig5OCg8Oig/PChCPihFQChIQihLRChORihRSChUSihXTChaTihdUChgUihjVChm" +
        "VihpWChsWihvXChyXih1YCh4Yih7ZCh+ZiiBaCiEaiiHbCiKbiiNcCiQciiTdCiWdiiZeCiceiiffCiifiilgCiogiirhCiuhiix" +
        "iCi0iii3jCi6jii9DiMAECMDEiMGFCMJFiMMGCMPGiMSHCMVHiMYICMbIiMeJCMhJiMkKCMnKiMqLCMtLiMwMCMzMiM2NCM5NiM8" +
        "OCM/OiNCPCNFPiNIQCNLQiNORCNRRiNUSCNXSiNaTCNdTiNgUCNjUiNmVCNpViNsWCNvWiNyXCN1XiN4YCN7YiN+ZCOBZiOEaCOH" +
        "aiOKbCONbiOQcCOTciOWdCOZdiOceCOfeiOifCOlfiOogCOrgiOuhCOxhiO0iCO3iiO6jCO9DB4ADh4DEB4GEh4JFB4MFh4PGB4S" +
        "Gh4VHB4YHh4bIB4eIh4hJB4kJh4nKB4qKh4tLB4wLh4zMB42Mh45NB48Nh4/OB5COh5FPB5IPh5LQB5OQh5RRB5URh5XSB5aSh5d" +
        "TB5gTh5jUB5mUh5pVB5sVh5vWB5yWh51XB54Xh57YB5+Yh6BZB6EZh6HaB6Kah6NbB6Qbh6TcB6Wch6ZdB6cdh6feB6ieh6lfB6o" +
        "fh6rgB6ugh6xhB60hh63iB66ih69ChkADBkDDhkGEBkJEhkMFBkPFhkSGBkVGhkYHBkbHhkeIBkhIhkkJBknJhkqKBktKhkwLBkz" +
        "Lhk2MBk5Mhk8NBk/NhlCOBlFOhlIPBlLPhlOQBlRQhlURBlXRhlaSBldShlgTBljThlmUBlpUhlsVBlvVhlyWBl1Whl4XBl7Xhl+" +
        "YBmBYhmEZBmHZhmKaBmNahmQbBmTbhmWcBmZchmcdBmfdhmieBmlehmofBmrfhmugBmxghm0hBm3hhm6iBm9CBQAChQDDBQGDhQJ" +
        "EBQMEhQPFBQSFhQVGBQYGhQbHBQeHhQhIBQkIhQnJBQqJhQtKBQwKhQzLBQ2LhQ5MBQ8MhQ/NBRCNhRFOBRIOhRLPBROPhRRQBRU" +
        "QhRXRBRaRhRdSBRgShRjTBRmThRpUBRsUhRvVBRyVhR1WBR4WhR7XBR+XhSBYBSEYhSHZBSKZhSNaBSQahSTbBSWbhSZcBScchSf" +
        "dBSidhSleBSoehSrfBSufhSxgBS0ghS3hBS6hhS9Bg8ACA8DCg8GDA8JDg8MEA8PEg8SFA8VFg8YGA8bGg8eHA8hHg8kIA8nIg8q" +
        "JA8tJg8wKA8zKg82LA85Lg88MA8/Mg9CNA9FNg9IOA9LOg9OPA9RPg9UQA9XQg9aRA9dRg9gSA9jSg9mTA9pTg9sUA9vUg9yVA91" +
        "Vg94WA97Wg9+XA+BXg+EYA+HYg+KZA+NZg+QaA+Tag+WbA+Zbg+ccA+fcg+idA+ldg+oeA+reg+ufA+xfg+0gA+3gg+6hA+9BAoA" +
        "BgoDCAoGCgoJDAoMDgoPEAoSEgoVFAoYFgobGAoeGgohHAokHgonIAoqIgotJAowJgozKAo2Kgo5LAo8Lgo/MApCMgpFNApINgpL" +
        "OApOOgpRPApUPgpXQApaQgpdRApgRgpjSApmSgppTApsTgpvUApyUgp1VAp4Vgp7WAp+WgqBXAqEXgqHYAqKYgqNZAqQZgqTaAqW" +
        "agqZbAqcbgqfcAqicgqldAqodgqreAquegqxfAq0fgq3gAq6ggq9AgUABAUDBgUGCAUJCgUMDAUPDgUSEAUVEgUYFAUbFgUeGAUh" +
        "GgUkHAUnHgUqIAUtIgUwJAUzJgU2KAU5KgU8LAU/LgVCMAVFMgVINAVLNgVOOAVROgVUPAVXPgVaQAVdQgVgRAVjRgVmSAVpSgVs" +
        "TAVvTgVyUAV1UgV4VAV7VgV+WAWBWgWEXAWHXgWKYAWNYgWQZAWTZgWWaAWZagWcbAWfbgWicAWlcgWodAWrdgWueAWxegW0fAW3" +
        "fgW6gAW9AAAAAgADBAAGBgAJCAAMCgAPDAASDgAVEAAYEgAbFAAeFgAhGAAkGgAnHAAqHgAtIAAwIgAzJAA2JgA5KAA8KgA/LABC" +
        "LgBFMABIMgBLNABONgBROABUOgBXPABaPgBdQABgQgBjRABmRgBpSABsSgBvTAByTgB1UAB4UgB7VAB+VgCBWACEWgCHXACKXgCN" +
        "YACQYgCTZACWZgCZaACcagCfbACibgClcACocgCrdACudgCxeAC0egC3fAC6fgC9";

    internal const string WebpFileFormat = "WEBP";
    internal const int WebpWidth = 64;
    internal const int WebpHeight = 32;
    internal const string WebpBase64 =
        "UklGRqIAAABXRUJQVlA4IJYAAADQBACdASpAACAAPm0ylEekIrAhJAySAA2JZC2AE2AbCJVTsgsbXMJ+EERGr//++AD+/uUC3+EK" +
        "alZoN/UsPvBqoyN13md81dhVfrrRW+DbT5ucIP9fdwq90niPX/nIXF2LocFhax63qsL/1k5f/01WFoqnFkzKpmPZNkWQ2fcnoUQz" +
        "qegIT2vC2ZN3T5OEFffjlaF7YAA=";

    internal static byte[] Png() { return Convert.FromBase64String(PngBase64); }
    internal static byte[] Jpeg() { return Convert.FromBase64String(JpegBase64); }
    internal static byte[] Gif() { return Convert.FromBase64String(GifBase64); }
    internal static byte[] Bmp() { return Convert.FromBase64String(BmpBase64); }
    internal static byte[] Webp() { return Convert.FromBase64String(WebpBase64); }
}
