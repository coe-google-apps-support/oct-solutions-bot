using AuthBot;
using AuthBot.Dialogs;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Connector;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;

namespace OctSolutions.bot.Dialogs
{
    // Although the name of the base AzureAuthDialog class suggests
    // it is for the Azure AD, it actually can be used for any OAuth 2.0 endpoint
    // So, we'll just inherit from it and give it a more appropriate name.
    // The other difference is we hide the constructor for resources since that 
    // is not applicable to Google.
    // Maybe in the future there will be some stuff specific to Google too.
    [Serializable]
    public class GoogleAuthDialog : AzureAuthDialog
    { 
        public GoogleAuthDialog(string[] scopes, string prompt = "Please click to sign in: ") : base(scopes, prompt)
        {

        }

        protected override async Task CheckForLogin(IDialogContext context, IMessageActivity msg)
        {
            try
            {
                string token;
                if (resourceId != null)
                    token = await context.GetAccessToken(resourceId);
                else
                    token = await context.GetAccessToken(scopes);

                if (string.IsNullOrEmpty(token))
                {
                    if (msg.Text != null &&
                        AuthBot.Models.CancellationWords.GetCancellationWords().Contains(msg.Text.ToUpper()))
                    {
                        context.Done(string.Empty);
                    }
                    else
                    {
                        var resumptionCookie = new ResumptionCookie(msg);

                        string authenticationUrl;
                        if (resourceId != null)
                            authenticationUrl = await AuthBot.Helpers.AzureActiveDirectoryHelper.GetAuthUrlAsync(resumptionCookie, resourceId);
                        else
                            //authenticationUrl = await AuthBot.Helpers.AzureActiveDirectoryHelper.GetAuthUrlAsync(resumptionCookie, scopes);
                            authenticationUrl = await GetAuthUrlAsync(resumptionCookie, scopes);

                        await PromptToLogin(context, msg, authenticationUrl);
                        context.Wait(this.MessageReceivedAsync);
                    }
                }
                else
                {
                    context.Done(string.Empty);
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        public override async Task MessageReceivedAsync(IDialogContext context, IAwaitable<IMessageActivity> argument)
        {
            var msg = await argument;

            AuthBot.Models.AuthResult authResult;
            string validated = "";
            int magicNumber = 0;
            if (context.UserData.TryGetValue(ContextConstants.AuthResultKey, out authResult))
            {
                try
                {
                    //IMPORTANT: DO NOT REMOVE THE MAGIC NUMBER CHECK THAT WE DO HERE. THIS IS AN ABSOLUTE SECURITY REQUIREMENT
                    //REMOVING THIS WILL REMOVE YOUR BOT AND YOUR USERS TO SECURITY VULNERABILITIES. 
                    //MAKE SURE YOU UNDERSTAND THE ATTACK VECTORS AND WHY THIS IS IN PLACE.
                    context.UserData.TryGetValue<string>(ContextConstants.MagicNumberValidated, out validated);
                    if (validated == "true")
                    {
                        context.Done($"Thanks {authResult.UserName}. You are now logged in. ");
                    }
                    else if (context.UserData.TryGetValue<int>(ContextConstants.MagicNumberKey, out magicNumber))
                    {
                        if (msg.Text == null)
                        {
                            await context.PostAsync($"Please paste back the number you received in your authentication screen.");

                            context.Wait(this.MessageReceivedAsync);
                        }
                        else
                        {

                            if (msg.Text.Length >= 6 && magicNumber.ToString() == msg.Text.Substring(0, 6))
                            {
                                context.UserData.SetValue<string>(ContextConstants.MagicNumberValidated, "true");
                                context.Done($"Thanks {authResult.UserName}. You are now logged in. ");
                            }
                            else
                            {
                                context.UserData.RemoveValue(ContextConstants.AuthResultKey);
                                context.UserData.SetValue<string>(ContextConstants.MagicNumberValidated, "false");
                                context.UserData.RemoveValue(ContextConstants.MagicNumberKey);
                                await context.PostAsync($"I'm sorry but I couldn't validate your number. Please try authenticating once again. ");

                                context.Wait(this.MessageReceivedAsync);
                            }
                        }
                    }
                }
                catch
                {
                    context.UserData.RemoveValue(ContextConstants.AuthResultKey);
                    context.UserData.SetValue(ContextConstants.MagicNumberValidated, "false");
                    context.UserData.RemoveValue(ContextConstants.MagicNumberKey);
                    context.Done($"I'm sorry but something went wrong while authenticating.");
                }
            }
            else
            {
                await this.CheckForLogin(context, msg);
            }
        }

        protected override Task PromptToLogin(IDialogContext context, IMessageActivity msg, string authenticationUrl)
        {
            Attachment plAttachment = null;
            switch (msg.ChannelId)
            {
                case "skypeforbusiness":
                    return context.PostAsync(this.prompt + "[Click here](" + authenticationUrl + ")");
                case "emulator":
                case "skype":
                    {
                        SigninCard plCard = new SigninCard(this.prompt, GetCardActions(authenticationUrl, "signin"));
                        plAttachment = plCard.ToAttachment();
                        break;
                    }
                // Teams does not yet support signin cards
                case "msteams":
                    {
                        ThumbnailCard plCard = new ThumbnailCard()
                        {
                            Title = this.prompt,
                            Subtitle = "",
                            Images = new List<CardImage>(),
                            Buttons = GetCardActions(authenticationUrl, "openUrl")
                        };
                        plAttachment = plCard.ToAttachment();
                        break;
                    }
                default:
                    {
                        SigninCard plCard = new SigninCard(this.prompt, GetCardActions(authenticationUrl, "signin"));
                        plAttachment = plCard.ToAttachment();
                        break;
                    }
                    //                    return context.PostAsync(this.prompt + "[Click here](" + authenticationUrl + ")");
            }

            IMessageActivity response = context.MakeMessage();
            response.Recipient = msg.From;
            response.Type = "message";

            response.Attachments = new List<Attachment>();
            response.Attachments.Add(plAttachment);

            return context.PostAsync(response);
        }

        private List<CardAction> GetCardActions(string authenticationUrl, string actionType)
        {
            List<CardAction> cardButtons = new List<CardAction>();
            CardAction plButton = new CardAction()
            {
                Value = authenticationUrl,
                Type = actionType,
                Title = "Authentication Required"
            };
            cardButtons.Add(plButton);
            return cardButtons;
        }


        protected virtual async Task<string> GetAuthUrlAsync(ResumptionCookie resumptionCookie, string[] scopes)
        {
            var extraParameters = BuildExtraParameters(resumptionCookie);
            Uri redirectUri = new Uri(AuthBot.Models.AuthSettings.RedirectUrl);
            if (string.Equals(AuthBot.Models.AuthSettings.Mode, "v2", StringComparison.OrdinalIgnoreCase))
            {
                var tokenCache = new AuthBot.Models.InMemoryTokenCacheMSAL();
                Microsoft.Identity.Client.ConfidentialClientApplication client = new Microsoft.Identity.Client.ConfidentialClientApplication("https://login.microsoftonline.com/" + AuthBot.Models.AuthSettings.Tenant + "/oauth2/v2.0",
                    AuthBot.Models.AuthSettings.ClientId, redirectUri.ToString(),
                    new Microsoft.Identity.Client.ClientCredential(AuthBot.Models.AuthSettings.ClientSecret),
                    tokenCache);


                //var uri = "https://login.microsoftonline.com/" + AuthSettings.Tenant + "/oauth2/v2.0/authorize?response_type=code" +
                //    "&client_id=" + AuthSettings.ClientId +
                //    "&client_secret=" + AuthSettings.ClientSecret +
                //    "&redirect_uri=" + HttpUtility.UrlEncode(AuthSettings.RedirectUrl) +
                //    "&scope=" + HttpUtility.UrlEncode("openid profile " + string.Join(" ", scopes)) +
                //    "&state=" + encodedCookie;


                var uri = await client.GetAuthorizationRequestUrlAsync(
                   scopes,
                    null,
                    $"state={extraParameters}");
                return uri.ToString();
            }
            else if (string.Equals(AuthBot.Models.AuthSettings.Mode, "b2c", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }
            return null;
        }

        private static string BuildExtraParameters(ResumptionCookie resumptionCookie)
        {
            var encodedCookie = UrlToken.Encode(resumptionCookie);

            //var queryString = HttpUtility.ParseQueryString(string.Empty);
            //queryString["userId"] = resumptionCookie.Address.UserId;
            //queryString["botId"] = resumptionCookie.Address.BotId;
            //queryString["conversationId"] = resumptionCookie.Address.ConversationId;
            //queryString["serviceUrl"] = resumptionCookie.Address.ServiceUrl;
            //queryString["channelId"] = resumptionCookie.Address.ChannelId;
            //queryString["locale"] = resumptionCookie.Locale ?? "en";

            //return TokenEncoder(queryString.ToString());
            return encodedCookie;
        }
    }
}