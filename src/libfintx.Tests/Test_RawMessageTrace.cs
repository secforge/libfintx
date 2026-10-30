using libfintx.FinTS.Message;
using Xunit;

namespace libfintx.Tests;

public class Test_RawMessageTrace
{
    // Request masking (PIN, TAN, user id) is covered by Test_TraceMasking

    [Fact]
    public void Response_IBANs_And_Long_Numbers_Keep_Last_Four()
    {
        var masked = FinTSMessage.MaskResponse("HIUPD:7:6:4+1234567890::280:12345678+DE02120300000000202051+DIKKU:5:2:3+5490027801433219++C:0,:EUR:20260226'");

        Assert.DoesNotContain("1234567890", masked);
        Assert.DoesNotContain("DE02120300000000202051", masked);
        Assert.DoesNotContain("5490027801433219", masked);
        Assert.Contains("XXXXXX7890", masked);
        Assert.Contains("XXXXXXXXXXXXXXXXXX2051", masked);
        Assert.Contains("XXXXXXXXXXXX3219", masked);
        Assert.Contains("20260226", masked); // dates stay readable
    }
}
