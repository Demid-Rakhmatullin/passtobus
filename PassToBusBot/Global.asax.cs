using System;
using System.Collections.Generic;
using System.Configuration;
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
            //GlobalConfiguration.Configuration.IncludeErrorDetailPolicy = IncludeErrorDetailPolicy.Always;

            var webhookUrl = ConfigurationManager.AppSettings["webhook_url"];
            GlobalConfiguration.Configuration.Properties.AddOrUpdate("Bot", Bot.Get(webhookUrl), (o1, o2) => { return Bot.Get(webhookUrl); });
            GlobalConfiguration.Configuration.Routes.MapHttpRoute("Webhook", webhookUrl, new { Controller = "Test", Action = "Webhook" });
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
        public static TelegramBotClient Get(string webhookUrl)
        {
            //return null;
            if (_bot != null) return _bot;
            var token = ConfigurationManager.AppSettings["bot_token"];
            _bot = new TelegramBotClient(token);
            //_bot.OnCallbackQuery += _bot_OnCallbackQuery;
            _bot.SetWebhookAsync("https://telegram.bot.buy-ticket.ru/" + webhookUrl).Wait();
            return _bot;
        }

        //private static void _bot_OnCallbackQuery(object sender, Telegram.Bot.Args.CallbackQueryEventArgs e)
        //{
        //    string s = "";
        //}
    }
}
