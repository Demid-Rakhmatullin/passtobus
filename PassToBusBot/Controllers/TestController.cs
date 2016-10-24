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
        private static readonly Dictionary<long, List<RideRequest>> _history = new Dictionary<long, List<RideRequest>>();


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
                if (update.CallbackQuery.Data == "showAll" && update.CallbackQuery.Message != null)
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
                        responseMessText, parseMode: ParseMode.Html, replyMarkup: GetShowTopKeyboard());

                }
                else if (update.CallbackQuery.Data == "showTop" && update.CallbackQuery.Message != null)
                {
                    hasRequest = _requests.TryGetValue(update.CallbackQuery.Message.Chat.Id,
                        out currentRequest);

                    if (!hasRequest)
                        return Ok();

                    var topRides = GetTopRides(currentRequest, client, agentId, secret);

                    if (string.IsNullOrEmpty(topRides))
                        return Ok();

                    await bot.EditMessageTextAsync(update.CallbackQuery.Message.Chat.Id,
                        update.CallbackQuery.Message.MessageId,
                        topRides, parseMode: ParseMode.Html, replyMarkup: GetShowAllKeyboard());
                }
                else if (update.CallbackQuery.Data.StartsWith("h_"))
                {
                    var index = int.Parse(update.CallbackQuery.Data.Replace("h_", ""));
                    if (!_history.ContainsKey(update.CallbackQuery.Message.Chat.Id))
                    {
                        await bot.SendTextMessageAsync(update.CallbackQuery.Message.Chat.Id, "Ивиняюсь, не могу повторить запрос из истории.");
                        await bot.SendTextMessageAsync(update.CallbackQuery.Message.Chat.Id,
                                "Откуда Вы хотите поехать? Например, Москва");

                        return Ok();
                    }

                    var history = _history[update.CallbackQuery.Message.Chat.Id];
                    if (history == null || history.Count <= index)
                    {
                        await bot.SendTextMessageAsync(update.CallbackQuery.Message.Chat.Id, "Ивиняюсь, не могу повторить запрос из истории.");
                        await bot.SendTextMessageAsync(update.CallbackQuery.Message.Chat.Id,
                                "Откуда Вы хотите поехать? Например, Москва");

                        return Ok();
                    }

                    var topRides = GetTopRides(history[index], client, agentId, secret);

                    if (string.IsNullOrEmpty(topRides))
                    {
                        await bot.SendTextMessageAsync(update.CallbackQuery.Message.Chat.Id, "Ивиняюсь, не могу повторить запрос из истории.");
                        await bot.SendTextMessageAsync(update.CallbackQuery.Message.Chat.Id,
                                "Откуда Вы хотите поехать? Например, Москва");

                        return Ok();
                    }

                    //_requests[update.CallbackQuery.Message.Chat.Id] = history[index];

                    //if (_history.ContainsKey(update.CallbackQuery.Message.Chat.Id))
                    //{
                    //    var list = _history[update.CallbackQuery.Message.Chat.Id];
                    //    list.Insert(0, history[index]);
                    //}
                    //else
                    //{
                    //    var list = new List<RideRequest>();
                    //    list.Add(history[index]);
                    //    _history.Add(update.Message.Chat.Id, list);
                    //}

                    await bot.SendTextMessageAsync(update.CallbackQuery.Message.Chat.Id,
                        topRides, parseMode: ParseMode.Html, replyMarkup: GetShowAllKeyboard());
                }
                else if (update.CallbackQuery.Data.StartsWith("cf_"))
                {
                    var cityId = int.Parse(update.CallbackQuery.Data.Replace("cf_", ""));

                    hasRequest = _requests.TryGetValue(update.CallbackQuery.Message.Chat.Id, out currentRequest);

                    if (!hasRequest || currentRequest.TempCities == null || currentRequest.TempCities.All(c => c.city_id != cityId))
                    {
                        await bot.SendTextMessageAsync(update.CallbackQuery.Message.Chat.Id, "Ивиняюсь, не могу найти город.");
                        await bot.SendTextMessageAsync(update.CallbackQuery.Message.Chat.Id,
                                "Откуда Вы хотите поехать? Например, Москва");

                        return Ok();
                    }

                    var fromCity = currentRequest.TempCities.First(c => c.city_id == cityId);
                    currentRequest.FromCity = fromCity;

                    await bot.SendTextMessageAsync(update.CallbackQuery.Message.Chat.Id,
                        string.Format("Отлично! Город отправления: {0}\nТеперь укажите город прибытия. Например, Симферополь.",
                        fromCity.city_title)/*, replyMarkup: GetResetRequestKeyboard(update.CallbackQuery.Message.Chat.Id)*/);
                }
                else if (update.CallbackQuery.Data.StartsWith("ct_"))
                {
                    var cityId = int.Parse(update.CallbackQuery.Data.Replace("ct_", ""));

                    _requests.TryGetValue(update.CallbackQuery.Message.Chat.Id, out currentRequest);

                    if (currentRequest.TempCities == null || currentRequest.TempCities.All(c => c.city_id != cityId))
                    {
                        await bot.SendTextMessageAsync(update.CallbackQuery.Message.Chat.Id, "Ивиняюсь, не могу найти город.");
                        await bot.SendTextMessageAsync(update.CallbackQuery.Message.Chat.Id,
                                "Укажите город прибытия. Например, Симферополь.");

                        return Ok();
                    }

                    var toCity = currentRequest.TempCities.First(c => c.city_id == cityId);
                    currentRequest.ToCity = toCity;

                    await bot.SendTextMessageAsync(update.CallbackQuery.Message.Chat.Id,
                     string.Format("Замечательно! Едем по маршруту {0} - {1}. \n Осталось указать дату. Например 20 августа.",
                     currentRequest.FromCity.city_title, toCity.city_title)/*, replyMarkup: GetResetRequestKeyboard(update.CallbackQuery.Message.Chat.Id)*/);
                }
            }          

            if (update.Message == null || string.IsNullOrWhiteSpace(update.Message.Text))
                return Ok();

            if (update.Message.Text == "/start")
            {
                if (_requests.ContainsKey(update.Message.Chat.Id))
                    _requests.Remove(update.Message.Chat.Id);

                var helloTemplate = @"
{0}, рад знакомству с Вами. Я постараюсь помочь Вам найти расписание и стоимость билетов на нужный вам рейс. Любые пожелания по моей работе Вы можете направить на адрес hellobot@buy-ticket.ru. Давайте приступим!";

                await bot.SendTextMessageAsync(update.Message.Chat.Id,
                    string.Format(helloTemplate, update.Message.From.FirstName), replyMarkup: GetResetRequestKeyboard(update.Message.Chat.Id));
                await bot.SendTextMessageAsync(update.Message.Chat.Id,
                    "Откуда Вы хотите поехать?");
                return Ok();
            }
            else if (update.Message.Text == "Начать новый поиск")
            {
                if (_requests.ContainsKey(update.Message.Chat.Id))
                    _requests.Remove(update.Message.Chat.Id);
                await bot.SendTextMessageAsync(update.Message.Chat.Id,
                    "Откуда Вы хотите поехать? Например, Москва", replyMarkup: GetResetRequestKeyboard(update.Message.Chat.Id));
                return Ok();
            }
            else if (update.Message.Text == "История поиска")
            {
                if (!_history.ContainsKey(update.Message.Chat.Id))
                    return await SendInvalidInputMessageAndReturn(bot, update, "Извиняюсь, я не запоминаю Вашу историю надолго. Пожалуйста, начните поиск заново.");

                var history = _history[update.Message.Chat.Id];
                if (history == null || !history.Any())
                    return await SendInvalidInputMessageAndReturn(bot, update, "Извиняюсь, я не запоминаю Вашу историю надолго. Пожалуйста, начните поиск заново.");

                await bot.SendTextMessageAsync(update.Message.Chat.Id,
                    "Вы искали:", replyMarkup: GetHistoryKeyboard(history));
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
                var cities = JsonConvert.DeserializeObject<CitiesResponse>(response.Content).data.city_list;
                if (cities.Count() == 0)
                {
                    return await SendInvalidInputMessageAndReturn(bot, update, "Город отправления не найден"/*, replyMarkup: GetResetRequestKeyboard(update.Message.Chat.Id)*/);
                }
                else if (cities.Count() > 1)
                {
                    if (currentRequest == null)
                    {
                        currentRequest = new RideRequest();
                        _requests.Add(update.Message.Chat.Id, currentRequest);
                    }
                    currentRequest.TempCities = cities;

                    await SendInvalidInputMessageAndReturn(bot, update, "Найдено больше одного города по Вашему запросу:", replyMarkup: GetCitiesKeyboard(cities, true));
                }
                else
                {
                    var fromCity = cities.FirstOrDefault();

                    if (currentRequest == null)
                    {
                        currentRequest = new RideRequest();
                        _requests.Add(update.Message.Chat.Id, currentRequest);
                    }
                    currentRequest.FromCity = fromCity;

                    await bot.SendTextMessageAsync(update.Message.Chat.Id,
                        string.Format("Отлично! Город отправления: {0}\nТеперь укажите город прибытия. Например, Симферополь.",
                        fromCity.city_title)/*, replyMarkup: GetResetRequestKeyboard(update.Message.Chat.Id)*/);
                }
            }
            else if(currentRequest.ToCity == null)
            {
                currentRequest.TempCities = null;
                var toCityName = update.Message.Text;

                var toCityRequest = new RestRequest("city/list/to", Method.GET);
                toCityRequest.AddParameter("query", toCityName); // adds to POST or URL querystring based on Method

                var hash = CalculateMD5Hash(
                    string.Format("agent_id={1}query={0}secret_key={2}", 
                    toCityName, agentId, secret));
                toCityRequest.AddParameter("hash", hash);
                toCityRequest.AddParameter("agent_id", agentId);

                var response = client.Execute(toCityRequest);
                var cities = JsonConvert.DeserializeObject<CitiesResponse>(response.Content).data.city_list;
                if (cities.Count() == 0)
                {
                    return await SendInvalidInputMessageAndReturn(bot, update, "Город отправления не найден"/*, replyMarkup: GetResetRequestKeyboard(update.Message.Chat.Id)*/);
                }
                else if (cities.Count() > 1)
                {
                    currentRequest.TempCities = cities;

                    await SendInvalidInputMessageAndReturn(bot, update, "Найдено больше одного города по Вашему запросу:", replyMarkup: GetCitiesKeyboard(cities, false));
                }
                else
                {
                    var toCity = cities.First();

                    currentRequest.ToCity = toCity;
                    await bot.SendTextMessageAsync(update.Message.Chat.Id,
                        string.Format("Замечательно! Едем по маршруту {0} - {1}. \n Осталось указать дату. Например 20 августа.",
                        currentRequest.FromCity.city_title, toCity.city_title)/*, replyMarkup: GetResetRequestKeyboard(update.Message.Chat.Id)*/);
                }
            }
            else
            {
                currentRequest.TempCities = null;
                var dateString = update.Message.Text;

                DateTime date;
                var parseResult = DateTime.TryParse(dateString, new CultureInfo("RU-ru"), DateTimeStyles.None, out date);
                if (!parseResult)
                    return await SendInvalidInputMessageAndReturn(bot, update, "Не получилось понять дату"/*,
                        replyMarkup: GetResetRequestKeyboard(update.Message.Chat.Id)*/);

                if (date < DateTime.UtcNow)
                    date.AddYears(1);

                currentRequest.Date = date;

                var topRides = GetTopRides(currentRequest, client, agentId, secret);

                if (string.IsNullOrEmpty(topRides))
                    return await SendInvalidInputMessageAndReturn(bot, update,
                        string.Format("{0}, к сожалению, у нас нет информации о расписании движения автобусов по Вашему запросу. Как только появится рейс, я обязательно Вам сообщу!",
                        update.Message.From.FirstName)/*, replyMarkup: GetResetRequestKeyboard(update.Message.Chat.Id)*/);

                if (_history.ContainsKey(update.Message.Chat.Id))
                {
                    var list = _history[update.Message.Chat.Id];
                    list.Insert(0, currentRequest);
                }
                else
                {
                    var list = new List<RideRequest>();
                    list.Add(currentRequest);
                    _history.Add(update.Message.Chat.Id, list);
                    if (_history.Count > 10)
                        _history.Remove(_history.Keys.First());
                }

                await bot.SendTextMessageAsync(update.Message.Chat.Id, topRides, 
                    parseMode: ParseMode.Html, replyMarkup: GetShowAllKeyboard());
                //await bot.SendTextMessageAsync(update.Message.Chat.Id, "", replyMarkup: GetResetRequestKeyboard(update.Message.Chat.Id));


                //bot.OnCallbackQuery += Bot_OnCallbackQuery;
                //_requests.Remove(update.Message.Chat.Id);
                //await bot.SendTextMessageAsync(update.Message.Chat.Id, "Для нового поиска введите город отправления");
            }
            return Ok();
        }

        private string GetTopRides(RideRequest currentRequest, RestClient client, string agentId, string secret)
        {
            var ridesRequest = new RestSharp.RestRequest("ride/list", Method.GET);
            ridesRequest.AddParameter("city_id_start", currentRequest.FromCity.city_id);
            ridesRequest.AddParameter("city_id_end", currentRequest.ToCity.city_id);
            ridesRequest.AddParameter("date", currentRequest.Date.Value.ToString("yyyy-MM-dd"));// adds to POST or URL querystring based on Method
                                                                           //request.AddUrlSegment("id", "123"); // replaces matching token in request.Resource

            var hash = CalculateMD5Hash(
                string.Format("agent_id={3}city_id_start={0}city_id_end={1}date={2}secret_key={4}",
                currentRequest.FromCity.city_id, currentRequest.ToCity.city_id,
                currentRequest.Date.Value.ToString("yyyy-MM-dd"), agentId, secret));
            ridesRequest.AddParameter("hash", hash);
            ridesRequest.AddParameter("agent_id", agentId);

            var response = client.Execute(ridesRequest);
            var rides = JsonConvert.DeserializeObject<RideResponse>(response.Content).data.ride_list;
            rides = rides.Where(r => r.place_cnt > 0).ToArray();

            if (!rides.Any())
                return null;

            var cheapest = rides.OrderBy(r => r.price_agent_max).First();
            var fastest = rides.OrderBy(r => (r.datetime_end - r.datetime_start).TotalMinutes).First();

            var responseMessText = "";
            responseMessText += "Самый дешевый:\n";
            responseMessText += RenderRideInfo(cheapest);
            responseMessText += "\n—---------------------------------------------------—\n";
            responseMessText += "Самый быстрый:\n";
            responseMessText += RenderRideInfo(fastest);

            return responseMessText;
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

<a href=""https://buy-ticket.ru/lk/reservation/{10}?ref=b"">Забронировать</a>
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

        private ReplyKeyboardMarkup GetResetRequestKeyboard(long chatId)
        {
            var buttons = new List<KeyboardButton>();
            buttons.Add(new KeyboardButton("Начать новый поиск"));

            if (_history.ContainsKey(chatId))
                if(_history[chatId].Any())
                    buttons.Add(new KeyboardButton("История поиска"));

            var buttonsModel = new KeyboardButton[buttons.Count][];
            for (int i = 0; i < buttons.Count; i++)
                buttonsModel[i] = new KeyboardButton[] { buttons[i] };
            //{
            //    new KeyboardButton[]
            //    {
            //        new KeyboardButton("Начать новый поиск"),
            //    },
            //};

            return new ReplyKeyboardMarkup(buttonsModel, true, false);
        }

        private InlineKeyboardMarkup GetShowAllKeyboard()
        {
            var buttons = new InlineKeyboardButton[]
                    {
                        new InlineKeyboardButton("Показать все", "showAll")
                    };
            return new InlineKeyboardMarkup(buttons);
        }

        private InlineKeyboardMarkup GetShowTopKeyboard()
        {
            var buttons = new InlineKeyboardButton[]
                    {
                        new InlineKeyboardButton("Показать рекомендованные", "showTop")
                    };
            return new InlineKeyboardMarkup(buttons);
        }

        private InlineKeyboardMarkup GetHistoryKeyboard(List<RideRequest> history)
        {
            var buttons = new List<InlineKeyboardButton>();
            for(int i = 0; i < history.Count; i++)
            {
                if (i > 10)
                    break;
                var r = history[i];
                var text = string.Format("{0} - {1} {2}",
                    r.FromCity.city_title, r.ToCity.city_title, r.Date.Value.ToString("dd MMMM", new CultureInfo("RU-ru")));
                var id = "h_" + i;
                buttons.Add(new InlineKeyboardButton(text, id));
            }
            return new InlineKeyboardMarkup(buttons.ToArray());
        }

        private InlineKeyboardMarkup GetCitiesKeyboard(IEnumerable<City> cities, bool from)
        {
            var buttons = new InlineKeyboardButton[cities.Count()][];
            int i = 0;
            foreach (var city in cities)
            {
                var text = city.city_title;
                if (!string.IsNullOrEmpty(city.region_title))
                    text += ", " + city.region_title;
                text += ", " + city.country_title; 
                var id = (from ? "cf" : "ct") + "_" + city.city_id;
                buttons[i]= new InlineKeyboardButton[] { new InlineKeyboardButton(text, id) };
                i++;
            }
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