using DM.Services.Community.BusinessProcesses.Account.Registration.Confirmation;
using DM.Services.Core.Configuration;
using DM.Services.Mail.Rendering.Assets;
using DM.Services.Mail.Rendering.Rendering;
using DM.Services.Mail.Rendering.ViewModels;
using DM.Services.Mail.Sender;
using DM.Tests.Core;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Language.Flow;
using System;
using System.Threading.Tasks;
using Xunit;

namespace DM.Services.Community.Tests;

public class RegistrationMailSenderShould : UnitTestBase
{
    private readonly Mock<IRenderer> renderer;
    private readonly ISetup<IRenderer, Task<string>> renderSetup;
    private readonly Mock<IMailSender> sender;
    private readonly RegistrationMailSender registrationMailSender;

    public RegistrationMailSenderShould()
    {
        renderer = Mock<IRenderer>();
        renderSetup = renderer
            .Setup(r => r.Render(It.IsAny<RegistrationConfirmationViewModel>()));

        sender = Mock<IMailSender>();
        sender
            .Setup(s => s.Send(It.IsAny<MailLetter>()))
            .Returns(Task.CompletedTask);

        var assetsProvider = Mock<IEmailAssetsProvider>();
        assetsProvider
            .Setup(a => a.GetLogo())
            .Returns(new LinkedResource { ContentId = "logo", MimeType = "image/png", Content = [] });

        var options = Mock<IOptions<IntegrationSettings>>();
        options.Setup(o => o.Value).Returns(new IntegrationSettings { WebUrl = "http://some.url.com" });

        registrationMailSender = new RegistrationMailSender(
            renderer.Object, sender.Object, assetsProvider.Object, options.Object);
    }

    [Fact]
    public async Task RenderConfirmationTemplate()
    {
        renderSetup.ReturnsAsync("renderResult");
        await registrationMailSender.Send("email", Guid.Parse("3cac234a-9c07-4103-9179-d00562a487ba"));
        renderer.Verify(r => r.Render(
            It.Is<RegistrationConfirmationViewModel>(vm =>
                vm.ConfirmationLinkUrl == "http://some.url.com/activate/3cac234a-9c07-4103-9179-d00562a487ba")), Times.Once);
    }

    [Fact]
    public async Task SendLetterWithRenderedTemplate()
    {
        renderSetup.ReturnsAsync("rendered letter");
        await registrationMailSender.Send("email", Guid.NewGuid());
        sender.Verify(s => s.Send(It.Is<MailLetter>(l =>
            l.Address == "email" &&
            l.Subject.Contains("DM.AM") &&
            l.Body == "rendered letter")), Times.Once);
    }
}
