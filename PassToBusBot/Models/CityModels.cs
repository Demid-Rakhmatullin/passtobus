using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace PassToBusBot.Models
{
    public class City
    {
        public int city_id { get; set; } //ID города
        public string city_title { get; set; } //Название города
        public string region_title { get; set; } //Название региона
        public string district_title { get; set; } //Название района
        public int country_id { get; set; } //ID страны
        public string country_title { get; set; }  //Название страны
        public string country_iso3166 { get; set; } //Код страны в формате ISO 3166-1 alpha-2
        public decimal lat { get; set; } //Широта
        public decimal lng { get; set; } //Долгота
    }

    public class CitiesResponse
    {
        public CitiesData data { get; set; }
    }

    public class CitiesData
    {
        public IEnumerable<City> city_list { get; set; }
    }
}