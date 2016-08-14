using Newtonsoft.Json;
using PassToBusBot.Models;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Web.Http;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace PassToBusBot.Controllers
{
    public class TestController : ApiController
    {
        private static readonly Dictionary<long, RideRequest> _requests = new Dictionary<long, RideRequest>();

        [HttpGet]
        [Route("api/test/cities")]
        public object Cities(string s)
        {
            var apiUrl = ConfigurationManager.AppSettings["api_url"];
            var agentId = ConfigurationManager.AppSettings["agent_id"];
            var secret = ConfigurationManager.AppSettings["secret"];


            var client = new RestClient(apiUrl);
            // client.Authenticator = new HttpBasicAuthenticator(username, password);

            var request = new RestRequest("city/list/from", Method.GET);
            request.AddParameter("query", s); // adds to POST or URL querystring based on Method
            //request.AddUrlSegment("id", "123"); // replaces matching token in request.Resource

            string hash = CalculateMD5Hash(
                string.Format("agent_id={1}query={0}secret_key={2}", s, agentId, secret));
            request.AddParameter("hash", hash);
            request.AddParameter("agent_id", agentId);

            // add parameters for all properties on an object
            //request.AddObject(object);

            // or just whitelisted properties
            //request.AddObject(object, "PersonId", "Name", ...);

            // easily add HTTP Headers
            //request.AddHeader("header", "value");

            // add files to upload (works with compatible verbs)
            //request.AddFile("file", path);

            // execute the request
            IRestResponse response = client.Execute(request);
            return response.Content;
        }

        [HttpPost]
        [Route("api/telegram/webhook")]
        public async Task<IHttpActionResult> Webhook(Update update)
        {
            var apiUrl = ConfigurationManager.AppSettings["api_url"];
            var agentId = ConfigurationManager.AppSettings["agent_id"];
            var secret = ConfigurationManager.AppSettings["secret"];

            object obot;
            GlobalConfiguration.Configuration.Properties.TryGetValue("Bot", out obot);
            var bot = (TelegramBotClient)obot;

            var client = new RestClient(apiUrl);

            RideRequest currentRequest;
            bool hasRequest;

            if (update.Type == UpdateType.CallbackQueryUpdate)
            {
                if(update.CallbackQuery.Data == "showAll" && update.CallbackQuery.Message != null)
                {
                    hasRequest = _requests.TryGetValue(update.CallbackQuery.Message.Chat.Id,
                        out currentRequest);

                    if (!hasRequest)
                        return Ok();

                    var date = currentRequest.Date.Value;

                    var ridesRequest = new RestSharp.RestRequest("ride/list", Method.GET);
                    ridesRequest.AddParameter("city_id_start", currentRequest.FromCity.city_id);
                    ridesRequest.AddParameter("city_id_end", currentRequest.ToCity.city_id);
                    ridesRequest.AddParameter("date", currentRequest.Date.Value.ToString("yyyy-MM-dd"));// adds to POST or URL querystring based on Method
                                                                                   //request.AddUrlSegment("id", "123"); // replaces matching token in request.Resource

                    var hash = CalculateMD5Hash(
                        string.Format("agent_id={3}city_id_start={0}city_id_end={1}date={2}secret_key={4}",
                        currentRequest.FromCity.city_id, currentRequest.ToCity.city_id, 
                        date.ToString("yyyy-MM-dd"), agentId, secret));
                    ridesRequest.AddParameter("hash", hash);
                    ridesRequest.AddParameter("agent_id", agentId);

                    var response = client.Execute(ridesRequest);
                    var rides = JsonConvert.DeserializeObject<RideResponse>(response.Content).data.ride_list;
                    rides = rides.Where(r => r.place_cnt > 0).ToArray();

                    if (!rides.Any())
                        return Ok();


                    var responseMessText = "";
                    for (int i = 0; i < rides.Length; i++)
                    {
                        var ride = rides[i];
                        responseMessText += string.Format("Рейс {0}:\n", i + 1);
                        responseMessText += RenderRideInfo(ride);
                        if (i != rides.Length - 1)
                            responseMessText += "\n—---------------------------------------------------—\n";
                    }

                    await bot.EditMessageTextAsync(update.CallbackQuery.Message.Chat.Id,
                        update.CallbackQuery.Message.MessageId,
                        responseMessText, parseMode: ParseMode.Html);

                }
            }

            if (update.Message == null || string.IsNullOrWhiteSpace(update.Message.Text))
                return Ok();

            if (update.Message.Text == "/start")
            {
                if (_requests.ContainsKey(update.Message.Chat.Id))
                    _requests.Remove(update.Message.Chat.Id);

                var helloTemplate = @"
{0}, привет!
Я помогу тебе найти рейсы на автобус  по России, Европе и странам СНГ.
🚐   🇷🇺 🇪🇺 🇺🇦";

                await bot.SendTextMessageAsync(update.Message.Chat.Id, 
                    string.Format(helloTemplate, update.Message.From.FirstName));
                await bot.SendTextMessageAsync(update.Message.Chat.Id,
                    "Откуда вы хотите поехать?");
                return Ok();
            }
            else if(update.Message.Text == "Начать новый поиск")
            {
                if (_requests.ContainsKey(update.Message.Chat.Id))
                    _requests.Remove(update.Message.Chat.Id);
                await bot.SendTextMessageAsync(update.Message.Chat.Id,
                    "Откуда вы хотите поехать?");
                return Ok();
            }

            hasRequest =_requests.TryGetValue(update.Message.Chat.Id, out currentRequest);

            if (!hasRequest || currentRequest.FromCity == null)
            {
                var fromCityName = update.Message.Text;

                var fromCityRequest = new RestRequest("city/list/from", Method.GET);
                fromCityRequest.AddParameter("query", fromCityName); // adds to POST or URL querystring based on Method

                var hash = CalculateMD5Hash(
                    string.Format("agent_id={1}query={0}secret_key={2}", 
                    fromCityName, agentId, secret));
                fromCityRequest.AddParameter("hash", hash);
                fromCityRequest.AddParameter("agent_id", agentId);

                var response = client.Execute(fromCityRequest);
                var fromCity = JsonConvert.DeserializeObject<CitiesResponse>(response.Content).data.city_list.FirstOrDefault();
                if (fromCity == null)
                    return await SendInvalidInputMessageAndReturn(bot, update, "Город отправления не найден");

                if (currentRequest == null)
                {
                    currentRequest = new RideRequest();
                    _requests.Add(update.Message.Chat.Id, currentRequest);
                }
                currentRequest.FromCity = fromCity;

                                  

                await bot.SendTextMessageAsync(update.Message.Chat.Id,
                 string.Format("Отлично! Город отправления: {0}\n Теперь укажи город прибытия.", 
                 fromCity.city_title), replyMarkup: GetResetRequestKeyboard());
            }
            else if(currentRequest.ToCity == null)
            {
                var toCityName = update.Message.Text;

                var toCityRequest = new RestRequest("city/list/to", Method.GET);
                toCityRequest.AddParameter("query", toCityName); // adds to POST or URL querystring based on Method

                var hash = CalculateMD5Hash(
                    string.Format("agent_id={1}query={0}secret_key={2}", 
                    toCityName, agentId, secret));
                toCityRequest.AddParameter("hash", hash);
                toCityRequest.AddParameter("agent_id", agentId);

                var response = client.Execute(toCityRequest);
                var toCity = JsonConvert.DeserializeObject<CitiesResponse>(response.Content).data.city_list.FirstOrDefault();
                if (toCity == null)
                    return await SendInvalidInputMessageAndReturn(bot, update, "Город прибытия не найден",
                        replyMarkup: GetResetRequestKeyboard());

                currentRequest.ToCity = toCity;
                await bot.SendTextMessageAsync(update.Message.Chat.Id,
                    string.Format("Замечательно! Едем по маршруту {0} - {1}. \n Осталось указать дату, например 20 августа.",
                    currentRequest.FromCity.city_title, toCity.city_title), replyMarkup: GetResetRequestKeyboard());
            }
            else
            {
                var dateString = update.Message.Text;

                DateTime date;
                var parseResult = DateTime.TryParse(dateString, new CultureInfo("RU-ru"), DateTimeStyles.None, out date);
                if (!parseResult)
                    return await SendInvalidInputMessageAndReturn(bot, update, "Не получилось понять дату",
                        replyMarkup: GetResetRequestKeyboard());

                currentRequest.Date = date;

                var ridesRequest = new RestSharp.RestRequest("ride/list", Method.GET);
                ridesRequest.AddParameter("city_id_start", currentRequest.FromCity.city_id);
                ridesRequest.AddParameter("city_id_end", currentRequest.ToCity.city_id);
                ridesRequest.AddParameter("date", date.ToString("yyyy-MM-dd"));// adds to POST or URL querystring based on Method
                                                                               //request.AddUrlSegment("id", "123"); // replaces matching token in request.Resource

                var hash = CalculateMD5Hash(
                    string.Format("agent_id={3}city_id_start={0}city_id_end={1}date={2}secret_key={4}",
                    currentRequest.FromCity.city_id, currentRequest.ToCity.city_id,
                    date.ToString("yyyy-MM-dd"), agentId, secret));
                ridesRequest.AddParameter("hash", hash);
                ridesRequest.AddParameter("agent_id", agentId);

                var response = client.Execute(ridesRequest);
                var rides = JsonConvert.DeserializeObject<RideResponse>(response.Content).data.ride_list;
                rides = rides.Where(r => r.place_cnt > 0).ToArray();

                if (!rides.Any())
                    return await SendInvalidInputMessageAndReturn(bot, update, "На эту дату рейсов нет",
                        replyMarkup: GetResetRequestKeyboard());

  

                var cheapest = rides.OrderBy(r => r.price_agent_max).First();
                var fastest = rides.OrderBy(r => (r.datetime_end - r.datetime_start).TotalMinutes).First();

                var responseMessText = "";
                responseMessText += "Самый дешевый:\n";
                responseMessText += RenderRideInfo(cheapest);
                responseMessText += "\n—---------------------------------------------------—\n";
                responseMessText += "Самый быстрый:\n";
                responseMessText += RenderRideInfo(fastest);

                //for (int i = 0; i < rides.Length; i++)
                //{
                //    var ride = rides[i];
                //    if (i != rides.Length - 1)
                //        responseMessText += Environment.NewLine + "—---------------------------------------------------—";
                //}

                await bot.SendTextMessageAsync(update.Message.Chat.Id, responseMessText, 
                    parseMode: ParseMode.Html, replyMarkup: GetShowAllKeyboard());
                //bot.OnCallbackQuery += Bot_OnCallbackQuery;
                //_requests.Remove(update.Message.Chat.Id);
                //await bot.SendTextMessageAsync(update.Message.Chat.Id, "Для нового поиска введите город отправления");
            }
            return Ok();
        }

        private string RenderRideInfo(RideSegmentShort ride)
        {
            string ridesListTemplate = @"
<b>Отправление:</b>
🏙   {0}
📍   {1}
📆   {2}
⌚️   {3}

<b>Прибытие:</b>
🏙   {4}
📍   {5}
📆   {6}
⌚️   {7}


💰 Стоимость билета: {8} руб.
🛋 Свободных мест: {9}

<a href=""https://buy-ticket.ru/lk/reservation/{10}"">Забронировать</a>
";
            var russianCulture = new CultureInfo("RU-ru");

            return string.Format(ridesListTemplate,
                ride.station_start.city_title, ride.station_start.station_title,
                ride.datetime_start.ToString("dd MMMM", russianCulture), ride.datetime_start.ToString("HH:mm", russianCulture),
                ride.station_end.city_title, ride.station_end.station_title,
                ride.datetime_end.ToString("dd MMMM", russianCulture), ride.datetime_end.ToString("HH:mm", russianCulture),
                Math.Ceiling(ride.price_agent_max), ride.place_cnt,
                ride.ride_segment_id);

        }

        private ReplyKeyboardMarkup GetResetRequestKeyboard()
        {
            var buttons = new KeyboardButton[][]
                    {
                        new KeyboardButton[]
                        {
                            new KeyboardButton("Начать новый поиск"),
                        },
                    };
            return new ReplyKeyboardMarkup(buttons, true);
        }

        private InlineKeyboardMarkup GetShowAllKeyboard()
        {
            var buttons = new InlineKeyboardButton[]
                    {
                        new InlineKeyboardButton("Показать все", "showAll")
                    };
            return new InlineKeyboardMarkup(buttons);
        }

        private async Task<IHttpActionResult> SendInvalidInputMessageAndReturn(TelegramBotClient bot, Update update, 
            string text, IReplyMarkup replyMarkup = null)
        {
            await bot.SendTextMessageAsync(update.Message.Chat.Id, 
                text, replyMarkup: replyMarkup);
            return Ok();
        }



        public string CalculateMD5Hash(string input)

        {

            // step 1, calculate MD5 hash from input

            MD5 md5 = System.Security.Cryptography.MD5.Create();

            byte[] inputBytes = System.Text.Encoding.UTF8.GetBytes(input);

            byte[] hash = md5.ComputeHash(inputBytes);

            // step 2, convert byte array to hex string

            StringBuilder sb = new StringBuilder();

            for (int i = 0; i < hash.Length; i++)

            {

                sb.Append(hash[i].ToString("X2"));

            }

            return sb.ToString().ToLowerInvariant();

        }

    }
}