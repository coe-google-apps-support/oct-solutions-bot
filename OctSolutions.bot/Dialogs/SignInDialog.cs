using System;
using System.Threading.Tasks;
using System.Web.Services.Description;
using AuthBot;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Connector;

namespace OctSolutions.bot.Dialogs
{
    [Serializable]
    public class SignInDialog<T> : IDialog<T>
    {
        private static readonly string[] scopes = { "" }; /* TODO; resourceId or scopes */
        private readonly IDialog<T> _next;

        public SignInDialog(IDialog<T> next)
        {
            _next = next;
        }



        public Task StartAsync(IDialogContext context)
        {
            context.Wait<T>(ProcessMessageAsync);

            return Task.CompletedTask;
        }

        public async Task ProcessMessageAsync(IDialogContext context, IAwaitable<T> item)
        {
            var message = await item;

            var token = await context.GetAccessToken(scopes);
            if (string.IsNullOrWhiteSpace(token))
            {
                // NO ACCESS TOKEN...GET IT
                await context.Forward(new GoogleAuthDialog(scopes), ProcessAuthResultAsync, message, System.Threading.CancellationToken.None);

            }
            else
            {

                // HAVE TOKEN - spit it out to screen
                await context.PostAsync(token);
                context.Wait<T>(ProcessMessageAsync);
            }
        }

        public async Task ProcessAuthResultAsync(IDialogContext context, IAwaitable<string> item)
        {
            var message = await item;
            // Thank you for signing in...
            await context.PostAsync(message);

            context.Wait<T>(ProcessMessageAsync);
        }
    }
}