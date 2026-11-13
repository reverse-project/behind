using System.Text;
using System.Text.Json;
using Behind.Core.WebApi;
using Microsoft.Extensions.FileProviders;

namespace Behind.Tests.CoreWebApi;

public class TwoFactorFrontendTests
{
    private static readonly IFileProvider Provider = EmbeddedFrontend.CreateFileProvider();

    private static string Read(string path)
    {
        var file = Provider.GetFileInfo(path);
        Assert.True(file.Exists, path);

        using var reader = new StreamReader(file.CreateReadStream(), Encoding.UTF8);
        return reader.ReadToEnd();
    }

    private static IReadOnlySet<string> Keys(string locale)
    {
        using var document = JsonDocument.Parse(Read($"locales/{locale}.json"));

        return document.RootElement.GetProperty("messages")
            .EnumerateObject()
            .Select(property => property.Name)
            .Where(name => name.StartsWith("twoFactor.", StringComparison.Ordinal) || name.StartsWith("auth.twoFactor.", StringComparison.Ordinal))
            .ToHashSet();
    }

    [Theory]
    [InlineData("controllers/twofactor.js")]
    [InlineData("viewmodels/twofactor.js")]
    [InlineData("views/twofactor.html")]
    [InlineData("viewmodels/twofactorchallenge.js")]
    [InlineData("viewmodels/recoverycodes.js")]
    [InlineData("components/twofactorchallenge.js")]
    [InlineData("components/twofactorchallenge.html")]
    [InlineData("components/recoverycodes.js")]
    [InlineData("components/recoverycodes.html")]
    [InlineData("components/totpenrollment.js")]
    [InlineData("components/totpenrollment.html")]
    [InlineData("models/webauthn.js")]
    [InlineData("models/textexport.js")]
    public void Provider_ContainsTheTwoFactorFile(string path)
    {
        Assert.True(Provider.GetFileInfo(path).Exists, path);
    }

    [Fact]
    public void App_RoutesTheTwoFactorPageAndRegistersTheComponents()
    {
        var app = Read("app.js");

        Assert.Contains("\"#/profile/two-factor\": \"controllers/twofactor\"", app);
        Assert.Contains("ko.components.register(\"two-factor-challenge\", { require: \"components/twofactorchallenge\" })", app);
        Assert.Contains("ko.components.register(\"recovery-codes\", { require: \"components/recoverycodes\" })", app);
        Assert.Contains("ko.components.register(\"totp-enrollment\", { require: \"components/totpenrollment\" })", app);
    }

    [Fact]
    public void Controller_RendersTheTwoFactorViewWithItsViewModel()
    {
        var controller = Read("controllers/twofactor.js");

        Assert.Contains("text!views/twofactor.html", controller);
        Assert.Contains("viewmodels/twofactor", controller);
    }

    [Fact]
    public void Http_ExposesTheDeleteVerb()
    {
        Assert.Contains("return request(\"DELETE\", url)", Read("models/http.js"));
    }

    [Theory]
    [InlineData("views/signin.html")]
    [InlineData("views/magiclink.html")]
    public void SignInPages_ShowTheTwoFactorChallengeAndHideTheFirstStep(string view)
    {
        var template = Read(view);

        Assert.Contains("<two-factor-challenge params=\"challenge: twoFactor\"></two-factor-challenge>", template);
        Assert.Contains("!twoFactor.active()", template);
    }

    [Theory]
    [InlineData("viewmodels/signin.js")]
    [InlineData("viewmodels/magiclink.js")]
    public void SignInViewModels_StartTheChallengeInsteadOfASession(string viewModel)
    {
        var source = Read(viewModel);

        Assert.Contains("viewmodels/twofactorchallenge", source);
        Assert.Contains("if (result.twoFactor)", source);
        Assert.Contains("self.twoFactor.begin(result.twoFactor)", source);
        Assert.Contains("self.twoFactor.complete(result)", source);
    }

    [Fact]
    public void MagicLink_ContinuesAfterTheChallengeLikeBefore()
    {
        var source = Read("viewmodels/magiclink.js");

        Assert.Contains("context.replace(result.lostPassword ? \"#/profile/password\" : \"#/\")", source);
        Assert.Contains("context.navigate(\"#/sign-in\")", source);
    }

    [Fact]
    public void SignIn_ContinuesAfterTheChallengeLikeBefore()
    {
        Assert.Contains("context.navigate(result.lostPassword ? \"#/profile/password\" : \"#/\")", Read("viewmodels/signin.js"));
    }

    [Fact]
    public void Auth_ReturnsTheChallengeInsteadOfStartingASession()
    {
        var auth = Read("models/auth.js");

        Assert.Contains("!!data && !!data.ticket && !data.accessToken", auth);
        Assert.Contains("twoFactor: challengeOf(response.data)", auth);
        Assert.Contains("session.start(data)", auth);
        Assert.Contains("result.recoveryCodes = data.recoveryCodes || []", auth);
        Assert.Contains("result.lostPassword = data.function === \"LostPassword\"", auth);
    }

    [Theory]
    [InlineData("/sign/in/two-factor/\" + path")]
    [InlineData("signInStep(\"totp\"")]
    [InlineData("signInStep(\"recovery\"")]
    [InlineData("\"/sign/in/two-factor/passkey/options\"")]
    [InlineData("signInStep(\"passkey\"")]
    [InlineData("\"/sign/in/two-factor/setup/totp\"")]
    [InlineData("signInStep(\"setup/totp/confirm\"")]
    [InlineData("\"/sign/in/two-factor/setup/passkey/options\"")]
    [InlineData("signInStep(\"setup/passkey\"")]
    [InlineData("http.get(\"/profile/two-factor\")")]
    [InlineData("http.post(\"/profile/two-factor/totp\")")]
    [InlineData("http.post(\"/profile/two-factor/totp/confirm\", { code: code })")]
    [InlineData("http.delete(\"/profile/two-factor/totp\")")]
    [InlineData("\"/profile/two-factor/passkeys/options\"")]
    [InlineData("http.post(\"/profile/two-factor/passkeys\", { ticket: ticket, credential: credential")]
    [InlineData("http.delete(\"/profile/two-factor/passkeys/\" + encodeURIComponent(id))")]
    [InlineData("http.post(\"/profile/two-factor/recovery-codes\")")]
    public void Auth_UsesTheTwoFactorEndpoints(string fragment)
    {
        Assert.Contains(fragment, Read("models/auth.js"));
    }

    [Fact]
    public void Auth_ExplainsTheFailuresOfTheSecondStep()
    {
        var auth = Read("models/auth.js");

        Assert.Contains("401: \"auth.twoFactor.ticketInvalid\"", auth);
        Assert.Contains("invalidCode: \"auth.twoFactor.invalidCode\"", auth);
        Assert.Contains("403: messageByCode({ invalidCode: \"auth.twoFactor.invalidCode\" }, bannedMessage)", auth);
        Assert.Contains("429: \"auth.tooManyAttempts\"", auth);
        Assert.Contains("required: \"auth.twoFactor.required\"", auth);
        Assert.Contains("result.restart = response.status === 401", auth);
    }

    [Fact]
    public void Auth_RendersTheQrCodeAsAnImageDataUriInsteadOfMarkup()
    {
        var auth = Read("models/auth.js");
        var template = Read("components/totpenrollment.html");

        Assert.Contains("\"data:image/svg+xml;charset=utf-8,\" + encodeURIComponent(data.qrCode)", auth);
        Assert.Contains("attr: { src: enrollment().image }", template);
        Assert.DoesNotContain("html:", template);
    }

    [Fact]
    public void WebAuthn_ConvertsBase64UrlAndCallsTheBrowser()
    {
        var model = Read("models/webauthn.js");

        Assert.Contains("navigator.credentials.create({ publicKey: creationOptions(options) })", model);
        Assert.Contains("navigator.credentials.get({ publicKey: requestOptions(options) })", model);
        Assert.Contains("replace(/\\+/g, \"-\").replace(/\\//g, \"_\")", model);
        Assert.Contains("attestationObject: toBase64Url(response.attestationObject)", model);
        Assert.Contains("authenticatorData: toBase64Url(response.authenticatorData)", model);
        Assert.Contains("userHandle: response.userHandle ? toBase64Url(response.userHandle) : null", model);
        Assert.Contains("getClientExtensionResults()", model);
        Assert.Contains("!!window.PublicKeyCredential", model);
        Assert.Contains("NotAllowedError: \"auth.twoFactor.passkeyCancelled\"", model);
    }

    [Fact]
    public void Challenge_OffersCodePasskeyRecoveryAndSetup()
    {
        var template = Read("components/twofactorchallenge.html");

        Assert.Contains("autocomplete=\"one-time-code\"", template);
        Assert.Contains("inputmode=\"numeric\"", template);
        Assert.Contains("maxlength=\"8\"", template);
        Assert.Contains("submit: verifyTotp", template);
        Assert.Contains("submit: verifyRecovery", template);
        Assert.Contains("click: usePasskey", template);
        Assert.Contains("click: toggleRecovery", template);
        Assert.Contains("click: startTotp", template);
        Assert.Contains("click: registerPasskey", template);
        Assert.Contains("click: back", template);
        Assert.Contains("visible: setupRequired", template);
        Assert.Contains("<totp-enrollment params=", template);
        Assert.Contains("<recovery-codes params=\"codes: recoveryCodes, done: proceed", template);
    }

    [Fact]
    public void ChallengeViewModel_HidesThePasskeyWhenTheBrowserCannotUseIt()
    {
        var viewModel = Read("viewmodels/twofactorchallenge.js");

        Assert.Contains("has(\"Passkey\") && webauthn.supported()", viewModel);
        Assert.Contains("has(\"Totp\")", viewModel);
        Assert.Contains("has(\"Recovery\")", viewModel);
        Assert.Contains("auth.twoFactor.verifyTotp(", viewModel);
        Assert.Contains("auth.twoFactor.verifyRecovery(", viewModel);
        Assert.Contains("auth.twoFactor.signInWithPasskey(", viewModel);
        Assert.Contains("auth.twoFactor.setupTotp(", viewModel);
        Assert.Contains("auth.twoFactor.confirmSetupTotp(", viewModel);
        Assert.Contains("auth.twoFactor.setupPasskey(", viewModel);
    }

    [Fact]
    public void EnrollmentSettings_HideOnlyEnrollmentAndPreserveFactorManagementAndChallenges()
    {
        var view = Read("views/twofactor.html");
        var viewModel = Read("viewmodels/twofactor.js");
        var challenge = Read("components/twofactorchallenge.html");
        var challengeViewModel = Read("viewmodels/twofactorchallenge.js");

        Assert.Contains("visible: canEnrollTotp() && !totpEnabled()", view);
        Assert.Contains("visible: passkeySupported && canEnrollPasskey()", view);
        Assert.Contains("visible: totpEnabled, enable: !busy(), click: removeTotp", view);
        Assert.Contains("click: $parent.removePasskey", view);
        Assert.Contains("accounts.twoFactor() && accounts.passkey()", viewModel);
        Assert.Contains("visible: canSetupTotp()", challenge);
        Assert.Contains("visible: canSetupPasskey", challenge);
        Assert.Contains("visible: passkeyAvailable() && !setupRequired()", challenge);
        Assert.Contains("accounts.twoFactor() && accounts.passkey() && self.passkeyAvailable()", challengeViewModel);
        Assert.Contains("auth.twoFactor.signInWithPasskey(", challengeViewModel);
    }

    [Fact]
    public void ChallengeViewModel_ShowsTheRecoveryCodesOnceBeforeContinuing()
    {
        var viewModel = Read("viewmodels/twofactorchallenge.js");

        Assert.Contains("signedIn.recoveryCodes.length > 0", viewModel);
        Assert.Contains("self.stage(\"codes\")", viewModel);
        Assert.Contains("options.done(result)", viewModel);
        Assert.Contains("self.recoveryCodes([])", viewModel);
    }

    [Fact]
    public void ChallengeViewModel_ReturnsToTheFirstStepWhenTheTicketIsInvalid()
    {
        var viewModel = Read("viewmodels/twofactorchallenge.js");

        Assert.Contains("if (result.restart)", viewModel);
        Assert.Contains("options.back(message)", viewModel);
        Assert.Contains("if (result.ticket)", viewModel);
    }

    [Fact]
    public void RecoveryCodes_AreCopiedAndDownloadedAsText()
    {
        var viewModel = Read("viewmodels/recoverycodes.js");
        var template = Read("components/recoverycodes.html");
        var export = Read("models/textexport.js");

        Assert.Contains("click: copy", template);
        Assert.Contains("click: download", template);
        Assert.Contains("click: done", template);
        Assert.Contains("foreach: codes", template);
        Assert.Contains("textexport.download(fileName, text())", viewModel);
        Assert.Contains("navigator.clipboard.writeText(text)", export);
        Assert.Contains("new Blob([text], { type: \"text/plain\" })", export);
    }

    [Fact]
    public void Page_ManagesTheAuthenticatorPasskeysAndRecoveryCodes()
    {
        var view = Read("views/twofactor.html");
        var viewModel = Read("viewmodels/twofactor.js");

        Assert.Contains("click: startTotp", view);
        Assert.Contains("click: removeTotp", view);
        Assert.Contains("submit: addPasskey", view);
        Assert.Contains("click: $parent.removePasskey", view);
        Assert.Contains("click: regenerateRecoveryCodes", view);
        Assert.Contains("visible: required", view);
        Assert.Contains("<recovery-codes params=\"codes: recoveryCodes, done: dismissCodes", view);
        Assert.Contains("visible: !isSignedIn()", view);
        Assert.Contains("auth.twoFactor.load()", viewModel);
        Assert.Contains("auth.twoFactor.confirmTotp(", viewModel);
        Assert.Contains("auth.twoFactor.addPasskey(", viewModel);
        Assert.Contains("auth.twoFactor.removePasskey(", viewModel);
        Assert.Contains("auth.twoFactor.regenerateRecoveryCodes", viewModel);
    }

    [Fact]
    public void Page_AsksForConfirmationBeforeRemovingOrRegenerating()
    {
        var viewModel = Read("viewmodels/twofactor.js");

        Assert.Contains("confirmed(\"twoFactor.totp.confirmRemove\")", viewModel);
        Assert.Contains("confirmed(\"twoFactor.passkeys.confirmRemove\", [passkey.name])", viewModel);
        Assert.Contains("confirmed(\"twoFactor.recovery.confirmGenerate\")", viewModel);
    }

    [Fact]
    public void Profile_LinksToTheTwoFactorPage()
    {
        Assert.Contains("href=\"#/profile/two-factor\"", Read("views/profile.html"));
    }

    [Fact]
    public void Styles_CoverTheQrCodeAndTheRecoveryCodes()
    {
        var css = Read("css/app.css");

        Assert.Contains(".behind-totp-qrcode", css);
        Assert.Contains(".behind-recovery-codes", css);
        Assert.Contains(".behind-recovery-code ", css);
    }

    [Fact]
    public void Locales_DefineTheSameTwoFactorTexts()
    {
        var english = Keys("en-US");

        Assert.NotEmpty(english);
        Assert.Equal(english.Order(), Keys("hu-HU").Order());
    }

    [Theory]
    [InlineData("twoFactor.title")]
    [InlineData("twoFactor.challenge.usePasskey")]
    [InlineData("twoFactor.challenge.useRecovery")]
    [InlineData("twoFactor.setup.required")]
    [InlineData("twoFactor.totp.confirmRemove")]
    [InlineData("twoFactor.codes.notice")]
    [InlineData("twoFactor.passkeys.confirmRemove")]
    [InlineData("twoFactor.recovery.confirmGenerate")]
    [InlineData("auth.twoFactor.ticketInvalid")]
    [InlineData("auth.twoFactor.invalidCode")]
    [InlineData("auth.twoFactor.required")]
    [InlineData("auth.twoFactor.passkeyUnsupported")]
    public void Locales_ContainTheKey(string key)
    {
        Assert.Contains(key, Keys("en-US"));
        Assert.Contains(key, Keys("hu-HU"));
    }
}
