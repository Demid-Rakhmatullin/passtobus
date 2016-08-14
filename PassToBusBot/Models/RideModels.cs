using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace PassToBusBot.Models
{
    public class RideResponse
    {
        public RideData data { get; set; }
    }

    public class RideData
    {
        public RideSegmentShort[] ride_list { get; set; }
    }

    public class RideSegmentShort
    {

        /// <summary>
        /// ID рейса
        /// </summary>
        public string ride_segment_id { get; set; }
        /// <summary>
        /// Уникальный ID поставщика контента.
        /// </summary>
        public int vendor_id { get; set; }
        /// <summary>
        /// Признак актуальности данных рейса.
        ///1 – Данные актуальны
        ///0 – Данные рейса могли устареть
        /// </summary>
        public int is_relevant { get; set; }
        /// <summary>
        /// Название маршрута по которому выполняется данный рейс
        /// </summary>
        public string route_name { get; set; }

        /// <summary>
        /// Расстояние от точки до точки (в км.). Может быть равно NULL
        /// </summary>
        public string distance { get; set; }
        /// <summary>
        /// Количество свободных мест
        /// </summary>
        public int place_cnt { get; set; }
        /// <summary>
        ///  Максимальное количество мест которое можно купить(забронировать) за один раз 16
        /// </summary>
        public int buy_place_cnt_max { get; set; }

        /// <summary>
        /// ID валюты Агента.см.Объект «Валюта»
        /// </summary>
        public int currency_agent_id { get; set; }
        /// <summary>
        /// Предварительный расчёт цены покупки билета в UNITIKI в валюте Агента. Включает сервисные сборы UNITIKI.Данную сумму необходимо будет перевести в UNITIKI.
        /// </summary>
        public decimal price_unitiki { get; set; }

        /// <summary>
        /// Предварительный расчёт агентского вознаграждения в валюте Агента.Данную сумму Агент получит в качестве вознаграждения за продажу билета.
        /// </summary>
        public decimal price_agent_fee { get; set; }

        /// <summary>
        /// Предварительный расчёт максимальной итоговой цены билета в валюте Агента.Цена билета для покупателя с учетом всех сборов не должна превышать этой суммы.   
        /// /// </summary>
        public decimal price_agent_max { get; set; }
        /// <summary>
        /// ID валюты Вендора. см.Объект «Валюта»
        /// </summary>
        public int currency_source_id { get; set; }
        /// <summary>
        /// Номинал цены билета на рейс в валюте Вендора (справочное)
        /// </summary>
        public decimal price_source_tariff { get; set; }
        /// <summary>
        /// Дата и время отправления.Время местное (!)
        /// </summary>
        public DateTime datetime_start { get; set; }
        /// <summary>
        /// Дата и время прибытия.Время местное (!)
        /// </summary>
        public DateTime datetime_end { get; set; }
        /// <summary>
        /// Станция отправления.см.Объект «Станция»
        /// </summary>
        public Station station_start { get; set; }
        /// <summary>
        /// Станция прибытия.см.Объект «Станция»
        /// </summary>
        public Station station_end { get; set; }
        /// <summary>
        /// Автобус.Значение не обязательное. Если не указан автобус, то NULL.см.Объект «Автобус»
        /// </summary>
        public object bus { get; set; }
        /// <summary>
        /// Информация о фактическом перевозчике
        /// </summary>
        public string carrier_title { get; set; }
    }
}