using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Http;
using System.Web.Routing;
using Telegram.Bot;

namespace PassToBusBot
{
    public class WebApiApplication : System.Web.HttpApplication
    {
        protected void Application_Start()
        {
            GlobalConfiguration.Configure(WebApiConfig.Register);
            GlobalConfiguration.Configuration.IncludeErrorDetailPolicy
= IncludeErrorDetailPolicy.Always;
            GlobalConfiguration.Configuration.Properties.AddOrUpdate("Bot", Bot.Get(), (o1, o2) => { return Bot.Get(); });
        }
    }

    public static class Bot
    {
        private static TelegramBotClient _bot;

        /// <summary>
        /// Получаем бота, а если он еще
        /// не инициализирован - инициализируем
        /// и возвращаем
        /// </summary>
        public static TelegramBotClient Get()
        {
            if (_bot != null) return _bot;
            _bot = new TelegramBotClient("222100974:AAEcl7Ko9rJFSnVxKmtVMRZyO3xw0B2WdLU");
            //_bot.OnCallbackQuery += _bot_OnCallbackQuery;
            //_bot.SetWebhook("https://passtobusbot.azurewebsites.net/api/telegram/webhook");
            return _bot;
        }

        //private static void _bot_OnCallbackQuery(object sender, Telegram.Bot.Args.CallbackQueryEventArgs e)
        //{
        //    string s = "";
        //}
    }
}
