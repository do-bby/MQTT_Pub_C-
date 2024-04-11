using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace mqttclient.models
{
    public class WeatherDataJsonModel
    {
        public ResponseModel? response { get; set; }
        public class ResponseModel
        {
            public ResponseBodyModel? body { get; set; }
            public class ResponseBodyModel
            {
                public string? dataType { get; set; }
                public ResponseItemsModel? items { get; set; }

                public class ResponseItemsModel
                {
                    public WeatherDataItemJsonModel[]? item { get; set; }
                }
            }
        }
    }

    public class WeatherDataItemJsonModel
    {
        public string? baseDate { get; set; }
        public string? baseTime { get; set; }
        public string? category { get; set; }
        public string? fcstDate { get; set; }
        public string? fcstTime { get;set; }
        public string? fcstValue { get; set; }
        public int? nx { get; set; }
        public int? ny { get; set; }
    }
}
