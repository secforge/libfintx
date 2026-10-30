using libfintx.FinTS.Message;
using Xunit;

namespace libfintx.Tests;

public class Test_RawMessageTrace
{
    [Theory]
    [InlineData("HNSHA:5:2+1234567++secret'", "HNSHA:5:2+1234567++***'")]
    [InlineData("HNSHA:5:2+1234567++secret:123456'", "HNSHA:5:2+1234567++***'")]
    [InlineData("HNSHA:5:2+1234567++?+se?'cr?:et:123456'HNHBS:6:1+1'", "HNSHA:5:2+1234567++***'HNHBS:6:1+1'")]
    [InlineData("HNHBS:6:1+1'", "HNHBS:6:1+1'")]
    public void Request_PIN_And_TAN_Are_Masked(string message, string expected)
    {
        Assert.Equal(expected, FinTSMessage.MaskRequest(message, string.Empty));
    }

    [Fact]
    public void Request_User_Id_Is_Masked_Wherever_It_Appears()
    {
        var masked = FinTSMessage.MaskRequest("HKIDN:3:2+280:12345678+?+user1+0+1'HNSHK:2:4+PIN:2+942+1+1+1::0+1+1:20260101:000000+1:999:1+6:10:16+280:12345678:?+user1:S:0:0'", "?+user1");

        Assert.DoesNotContain("user1", masked);
    }

    [Fact]
    public void Request_Empty_User_Id_Changes_Nothing_But_HNSHA()
    {
        Assert.Equal("HKIDN:3:2+280:12345678+user+0+1'", FinTSMessage.MaskRequest("HKIDN:3:2+280:12345678+user+0+1'", string.Empty));
    }

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
