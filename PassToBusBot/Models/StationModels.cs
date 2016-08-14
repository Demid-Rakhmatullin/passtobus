using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace PassToBusBot.Models
{
    public class Station
    {
        public int station_id { get; set; } //ID станции
        public string station_title { get; set; } //Название станции
        public int city_id { get; set; }//ID города
        public string city_title { get; set; } //Название города
        public string region_title { get; set; } //Название региона
        public string district_title { get; set; } //Название района
        public int country_id { get; set; } //ID страны
        public string country_title { get; set; } //Название страны
        public string country_iso { get; set; } //Код страны в формате ISO 3166-1 alpha-2
        public decimal lat { get; set; } //Широта
        public decimal lng { get; set; } //Долгота
    }
}