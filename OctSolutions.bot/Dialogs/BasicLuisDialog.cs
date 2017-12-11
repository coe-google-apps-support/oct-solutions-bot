using System;
using System.Configuration;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Builder.Luis;
using Microsoft.Bot.Builder.Luis.Models;
using Microsoft.Bot.Connector;

namespace OctSolutions.bot.Dialogs
{
    [Serializable]
    public class BasicLuisDialog : LuisDialog<object>
    {
        public BasicLuisDialog() : base(new LuisService(new LuisModelAttribute(ConfigurationManager.AppSettings["LuisAppId"], ConfigurationManager.AppSettings["LuisAPIKey"])))
        {
        }


        [LuisIntent("None")]
        public async Task NoneIntent(IDialogContext context, LuisResult result)
        {
            await context.PostAsync($"You have reached the none intent. You said: {result.Query}"); //
            context.Wait(MessageReceived);
        }

        // Go to https://luis.ai and create a new intent, then train/publish your luis app.
        // Finally replace "MyIntent" with the name of your newly created intent in the following handler
        [LuisIntent("NewDevice")]
        public async Task NewDeviceIntent(IDialogContext context, LuisResult result)
        {
            if (result.Entities.Any(x => x.Type == "Device" && x.Entity == "ipad"))
            {
                await context.PostAsync($"So you want an iPad, eh?");
            }
            else
            {
                await context.PostAsync($"You have reached the NewDevice intent. You said: {result.Query}"); //                
            }
            context.Wait(MessageReceived);
        }
    }
}