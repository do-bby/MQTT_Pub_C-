using Microsoft.VisualBasic;
using mqttclient.models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Data;
using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Intrinsics.X86;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Xml;
using uPLibrary.Networking.M2Mqtt;
using uPLibrary.Networking.M2Mqtt.Messages;


class Program
{
    static string uID;
    static async Task InituID(){
        uID = await getUID();
    }
    // 기상 api service key : ClSg8YETAqDKd35FklryZ%2BYPkYWY6Q24PTpGdagqszHjRSWzowl08EhLqpdZKJvBkIUv%2FmrEcPwIK3iV8I3QVQ%3D%3D
    // 해양 api service key : EDrcCIFdi7qEsdqj0mgrjQ==
    static async Task Main(string[] args){
        string brokerAddress = "139.150.83.249";
        int brokerPort = 1883;
        await InituID();
        string username = "root";
        string password = "public";
        MqttClient client = new MqttClient(brokerAddress,brokerPort,false,null,null,MqttSslProtocols.None);
        client.Connect(Guid.NewGuid().ToString(),username,password);
        Task heartbeatTask = Task.Run(() => PublishHeartbeat(client));
        while (true)
        {
            // await Publishing(client);
            // await Publishing2(client);
            // await Publishing3(client);
            // await Publishing_ocean(client);
            // await Publishing_oceandata(client);
            // await Publishing_UV(client);
            // await Publishing_ATMO(client);
            // await Publishing_WAVE(client);
            await getWthrWrnMsg(client);
            await getLunPhInfo(client);
            await gettideObsPreTab(client);
            await Task.Delay(600000); // 10 minute
        }
        
//      string msg = "Hi2";
//      client.Publish(topic, System.Text.Encoding.UTF8.GetBytes(msg), MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE, false);

    }
    static async Task<string> getUID()
    {
        using(HttpClient client = new HttpClient())
        {
            string url = "https://pohang.ictpeople.co.kr/api/Equipment/GetEquipment?SerialNo=DX20240220-0001";
            HttpResponseMessage res = await client.GetAsync(url);
            try{
                HttpResponseMessage response = await client.GetAsync(url);
                if(response.IsSuccessStatusCode){
                    string responseBody = await response.Content.ReadAsStringAsync();
                    JObject json = JObject.Parse(responseBody);
                    JArray arr = (JArray)json["data"];
                    JObject obj = (JObject) arr[0];
                    string data = (string)obj["serialNo"];
                    return data;
                }
                else{
                    return null;
                }
            } catch(HttpRequestException e){
                Console.WriteLine("fail" + e.Message);
                return null;
            }
        }
    }
    static async Task PublishHeartbeat(MqttClient mqttclient)
    {
        while (true)
        {
            try
            {
                string obsrValue = "HeartBeat Data";
                string topic = "PohangPG/"+uID+"/heartbeat";
                mqttclient.Publish(topic, Encoding.UTF8.GetBytes(obsrValue), MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE, false);                
                Console.WriteLine("Heartbeat published");
                
            }
            catch (Exception e)
            {
                Console.WriteLine("Error publishing heartbeat: " + e.Message);
            }
            
            await Task.Delay(60000); // 1 minute delay
        }
    }
    //초단기실황 API
    static async Task Publishing(MqttClient mqttclient)
    {
        using (HttpClient client = new HttpClient())
        {
            try
            {
                //string topic = "PohangPG/uID/rain";
                string currentDay = DateTime.Now.ToString("yyyyMMdd");
                string currentTime = DateTime.Now.ToString("HHmm");
                //포항 송도 nx, ny = 54, 123
                string url = "https://apis.data.go.kr/1360000/VilageFcstInfoService_2.0/getUltraSrtNcst?serviceKey=ClSg8YETAqDKd35FklryZ%2BYPkYWY6Q24PTpGdagqszHjRSWzowl08EhLqpdZKJvBkIUv%2FmrEcPwIK3iV8I3QVQ%3D%3D&numOfRows=10&pageNo=1&base_date="+currentDay+"&base_time="+currentTime+"&nx=102&ny=94&&dataType=JSON";
                HttpResponseMessage res = await client.GetAsync(url);                
                if (res.IsSuccessStatusCode)
                {
                    //Console.WriteLine(tm);
                    string data = await res.Content.ReadAsStringAsync();                  
                    if (data != null)
                    {                
                        //강수량, 온도 등 모든 기상 데이터 PUBLISH.
                        // mqttclient.Publish(topic, Encoding.UTF8.GetBytes(data), MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE, false);
                        // Console.WriteLine("data: " + data);

                        //Ex) PTY(강수상태)인 데이터만 PUBLISH 
                        var parsedData = JsonConvert.DeserializeObject<dynamic>(data);
                        var items = parsedData.response.body.items.item;
                        foreach(var item in items){
                            if (item.category == "PTY"){
                                string obsrValue = item.ToString();
                                string topic = "PohangPG/34682a43-5ff2-4768-b50b-abfc2fc14729/rainNcst";
                                mqttclient.Publish(topic, Encoding.UTF8.GetBytes(obsrValue), MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE, false);
                                //heartbeat data publish
                                Console.WriteLine("강수형태: " + obsrValue);
                            }
                            else if (item.category == "REH"){
                                string obsrValue = item.ToString();                       
                                string topic = "PohangPG/34682a43-5ff2-4768-b50b-abfc2fc14729/humNcst";
                                mqttclient.Publish(topic, Encoding.UTF8.GetBytes(obsrValue), MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE, false);
                                Console.WriteLine("습도: " + obsrValue + "%");
                            }
                            else if (item.category == "RN1"){
                                string obsrValue = item.ToString();         
                                string topic = "PohangPG/34682a43-5ff2-4768-b50b-abfc2fc14729/hourRainNcst";
                                mqttclient.Publish(topic, Encoding.UTF8.GetBytes(obsrValue), MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE, false);
                                Console.WriteLine("1시간 강수량: " + obsrValue);
                            }
                            else if (item.category == "T1H"){
                                string obsrValue = item.ToString();       
                                string topic = "PohangPG/34682a43-5ff2-4768-b50b-abfc2fc14729/tempNcst";
                                mqttclient.Publish(topic, Encoding.UTF8.GetBytes(obsrValue), MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE, false);
                                Console.WriteLine("기온: " + obsrValue);
                            }                                                        
                            else if (item.category == "UUU"){
                                string obsrValue = item.ToString();    
                                string topic = "PohangPG/34682a43-5ff2-4768-b50b-abfc2fc14729/weWindNcst";
                                mqttclient.Publish(topic, Encoding.UTF8.GetBytes(obsrValue), MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE, false);
                                Console.WriteLine("동서바람성분 " + obsrValue);
                            }
                            else if (item.category == "VEC"){
                                string obsrValue = item.ToString();
                                string topic = "PohangPG/34682a43-5ff2-4768-b50b-abfc2fc14729/windDirNcst";
                                mqttclient.Publish(topic, Encoding.UTF8.GetBytes(obsrValue), MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE, false);
                                Console.WriteLine("풍향: " + obsrValue);
                            }
                            else if (item.category == "VVV"){
                                string obsrValue = item.ToString();
                                string topic = "PohangPG/34682a43-5ff2-4768-b50b-abfc2fc14729/snWindNcst";
                                mqttclient.Publish(topic, Encoding.UTF8.GetBytes(obsrValue), MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE, false);
                                Console.WriteLine("남북바람성분: " + obsrValue);
                            }                                                                                  
                            else if (item.category == "WSD"){
                                string obsrValue = item.ToString();
                                string topic = "PohangPG/34682a43-5ff2-4768-b50b-abfc2fc14729/windSpeedNcst";
                                mqttclient.Publish(topic, Encoding.UTF8.GetBytes(obsrValue), MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE, false);
                                Console.WriteLine("풍속: " + obsrValue);
                            }
                            // else{
                            //     string obsrValue = "HeartBeat Data";
                            //     string topic = "PohangPG/aef6d12a-6355-4e42-a28c-0773fb7f32fa/heartbeat";
                            //     mqttclient.Publish(topic, Encoding.UTF8.GetBytes(obsrValue), MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE, false);
                            // }                            
                        }                        
                    }
                    else{
                        Console.WriteLine("api data error");    
                    }
                }
                else
                {                    
                    Console.WriteLine("error: " + res.StatusCode);
                    //mqttclient.Publish(topic, "fail", MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE, false);
                }
                //heartbeat Data publish
                //mqttclient.Publish(topic, Encoding.UTF8.GetBytes("heartbeat"), MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE, false);

            }
            catch (Exception e)
            {
                //throw;
                string obsrValue = "HeartBeat Data";                
                string topic = "PohangPG/34682a43-5ff2-4768-b50b-abfc2fc14729/heartbeat";
                mqttclient.Publish(topic, Encoding.UTF8.GetBytes(obsrValue), MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE, false);
                //Console.WriteLine("error" + e.Message);
            }

        }
    }
    //초단기예보 API
    static async Task Publishing2(MqttClient mqttclient)
{
    using (HttpClient client = new HttpClient())
    {
        try
        {
            //string topic = "PohangPG/uID/rain";
            string currentDay = DateTime.Now.ToString("yyyyMMdd");
            string currentTime = DateTime.Now.ToString("HHmm");
            string url = "https://apis.data.go.kr/1360000/VilageFcstInfoService_2.0/getUltraSrtFcst?serviceKey=ClSg8YETAqDKd35FklryZ%2BYPkYWY6Q24PTpGdagqszHjRSWzowl08EhLqpdZKJvBkIUv%2FmrEcPwIK3iV8I3QVQ%3D%3D&numOfRows=60&pageNo=1&base_date="+currentDay+"&base_time="+currentTime+"&nx=102&ny=94&&dataType=JSON";
            HttpResponseMessage res = await client.GetAsync(url);                
            if (res.IsSuccessStatusCode)
            {
                //Console.WriteLine(tm);
                string data = await res.Content.ReadAsStringAsync();                  
                if (data != null)
                {                
                    var parsedData = JsonConvert.DeserializeObject<dynamic>(data);                    
                    var items = parsedData.response.body.items.item;
                    for (int i = 5; i<items.Count; i+=6){                        
                        JObject item = items[i];
                        string category = (string)item["category"];
                        switch (category) {
                            case "SKY":
                                string obsrValue = item.ToString();
                                string topic = "PohangPG/34682a43-5ff2-4768-b50b-abfc2fc14729/skyFcst";
                                mqttclient.Publish(topic, Encoding.UTF8.GetBytes(obsrValue), MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE, false);                                
                                break;
                            case "LGT":
                                string obsrValue2 = item.ToString();
                                string topic2 = "PohangPG/34682a43-5ff2-4768-b50b-abfc2fc14729/lgtFcst";
                                //Console.WriteLine(obsrValue2);
                                mqttclient.Publish(topic2, Encoding.UTF8.GetBytes(obsrValue2), MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE, false);                                
                                break;
                            case "PTY":
                                string obsrValue3 = item.ToString();
                                string topic3 = "PohangPG/34682a43-5ff2-4768-b50b-abfc2fc14729/rainFcst";
                                //onsole.WriteLine(obsrValue3);
                                mqttclient.Publish(topic3, Encoding.UTF8.GetBytes(obsrValue3), MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE, false);                                
                                break;
                            case "REH":
                                string obsrValue4 = item.ToString();
                                string topic4 = "PohangPG/34682a43-5ff2-4768-b50b-abfc2fc14729/humFcst";
                                mqttclient.Publish(topic4, Encoding.UTF8.GetBytes(obsrValue4), MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE, false);                                
                                break;
                            case "RN1":
                                string obsrValue5 = item.ToString();
                                string topic5 = "PohangPG/34682a43-5ff2-4768-b50b-abfc2fc14729/hourRainFcst";
                                mqttclient.Publish(topic5, Encoding.UTF8.GetBytes(obsrValue5), MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE, false);                                
                                break;
                            case "T1H":
                                string obsrValue6 = item.ToString();
                                string topic6 = "PohangPG/34682a43-5ff2-4768-b50b-abfc2fc14729/tempFcst";
                                mqttclient.Publish(topic6, Encoding.UTF8.GetBytes(obsrValue6), MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE, false);                                
                                break;
                            case "UUU":
                                string obsrValue7 = item.ToString();
                                string topic7 = "PohangPG/34682a43-5ff2-4768-b50b-abfc2fc14729/weWindFcst";
                                mqttclient.Publish(topic7, Encoding.UTF8.GetBytes(obsrValue7), MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE, false);                                
                                break;
                            case "VEC":
                                string obsrValue8 = item.ToString();
                                string topic8 = "PohangPG/34682a43-5ff2-4768-b50b-abfc2fc14729/windDirFcst";
                                mqttclient.Publish(topic8, Encoding.UTF8.GetBytes(obsrValue8), MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE, false);                                
                                break;
                            case "VVV":
                                string obsrValue9 = item.ToString();
                                string topic9 = "PohangPG/34682a43-5ff2-4768-b50b-abfc2fc14729/snWindFcst";
                                mqttclient.Publish(topic9, Encoding.UTF8.GetBytes(obsrValue9), MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE, false);                                
                                break;
                            case "WSD":
                                string obsrValue10 = item.ToString();
                                string topic10 = "PohangPG/34682a43-5ff2-4768-b50b-abfc2fc14729/windSpeedFcst";
                                mqttclient.Publish(topic10, Encoding.UTF8.GetBytes(obsrValue10), MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE, false);
                                break;    
                            }
                    }
                }
            }
            else
            {                    
                Console.WriteLine("error: " + res.StatusCode);
                //mqttclient.Publish(topic, "fail", MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE, false);
            }
            //heartbeat Data publish
            //mqttclient.Publish(topic, Encoding.UTF8.GetBytes("heartbeat"), MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE, false);
        }
        catch (Exception e)
        {
            //throw;
            string obsrValue = "HeartBeat Data";                
            string topic = "PohangPG/34682a43-5ff2-4768-b50b-abfc2fc14729/heartbeat";
            mqttclient.Publish(topic, Encoding.UTF8.GetBytes(obsrValue), MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE, false);
            //Console.WriteLine("error" + e.Message);
        }
    }
}
//단기  예보 API
static async Task Publishing3(MqttClient mqttclient)
{
    using (HttpClient client = new HttpClient())
    {
        try
        {
            string currentDay = DateTime.Now.ToString("yyyyMMdd");
            string currentTime = DateTime.Now.ToString("HHmm");
            string url = "https://apis.data.go.kr/1360000/VilageFcstInfoService_2.0/getUltraSrtFcst?serviceKey=ClSg8YETAqDKd35FklryZ%2BYPkYWY6Q24PTpGdagqszHjRSWzowl08EhLqpdZKJvBkIUv%2FmrEcPwIK3iV8I3QVQ%3D%3D&numOfRows=60&pageNo=1&base_date="+currentDay+"&base_time="+currentTime+"&nx=102&ny=94&&dataType=JSON";
            HttpResponseMessage res = await client.GetAsync(url);                
            if (res.IsSuccessStatusCode)
            {
                //Console.WriteLine(tm);
                string data = await res.Content.ReadAsStringAsync();                  
                if (data != null)
                {                
                    var parsedData = JsonConvert.DeserializeObject<dynamic>(data);                    
                    var items = parsedData.response.body.items.item;
                    for (int i = 0; i<items.Count; i++){
                        JObject item = items[i];
                        string category = (string)item["category"];
                        switch (category) {
                            case "POP":
                                string obsrValue = item.ToString();
                                string topic = "PohangPG/34682a43-5ff2-4768-b50b-abfc2fc14729/POPVF";
                                mqttclient.Publish(topic, Encoding.UTF8.GetBytes(obsrValue), MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE, false);                                
                                break;
                            case "PTY":
                                string obsrValue2 = item.ToString();
                                string topic2 = "PohangPG/34682a43-5ff2-4768-b50b-abfc2fc14729/PTYVF";
                                //Console.WriteLine(obsrValue2);
                                mqttclient.Publish(topic2, Encoding.UTF8.GetBytes(obsrValue2), MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE, false);                                
                                break;
                            case "PCP":
                                string obsrValue3 = item.ToString();
                                string topic3 = "PohangPG/34682a43-5ff2-4768-b50b-abfc2fc14729/PCPVF";
                                //onsole.WriteLine(obsrValue3);
                                mqttclient.Publish(topic3, Encoding.UTF8.GetBytes(obsrValue3), MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE, false);                                
                                break;
                            case "REH":
                                string obsrValue4 = item.ToString();
                                string topic4 = "PohangPG/34682a43-5ff2-4768-b50b-abfc2fc14729/REHVF";
                                mqttclient.Publish(topic4, Encoding.UTF8.GetBytes(obsrValue4), MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE, false);                                
                                break;
                            case "SNO":
                                string obsrValue5 = item.ToString();
                                string topic5 = "PohangPG/34682a43-5ff2-4768-b50b-abfc2fc14729/SNOVF";
                                mqttclient.Publish(topic5, Encoding.UTF8.GetBytes(obsrValue5), MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE, false);                                
                                break;
                            case "SKY":
                                string obsrValue6 = item.ToString();
                                string topic6 = "PohangPG/34682a43-5ff2-4768-b50b-abfc2fc14729/SKYVF";
                                mqttclient.Publish(topic6, Encoding.UTF8.GetBytes(obsrValue6), MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE, false);                                
                                break;
                            case "TMP":
                                string obsrValue7 = item.ToString();
                                string topic7 = "PohangPG/34682a43-5ff2-4768-b50b-abfc2fc14729/TMPVF";
                                mqttclient.Publish(topic7, Encoding.UTF8.GetBytes(obsrValue7), MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE, false);                                
                                break;
                            case "TMN":
                                string obsrValue8 = item.ToString();
                                string topic8 = "PohangPG/34682a43-5ff2-4768-b50b-abfc2fc14729/TMNVF";
                                mqttclient.Publish(topic8, Encoding.UTF8.GetBytes(obsrValue8), MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE, false);                                
                                break;
                            case "TMX":
                                string obsrValue9 = item.ToString();
                                string topic9 = "PohangPG/34682a43-5ff2-4768-b50b-abfc2fc14729/TMXVF";
                                mqttclient.Publish(topic9, Encoding.UTF8.GetBytes(obsrValue9), MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE, false);                                
                                break;
                            case "UUU":
                                string obsrValue10 = item.ToString();
                                string topic10 = "PohangPG/34682a43-5ff2-4768-b50b-abfc2fc14729/UUUVF";
                                mqttclient.Publish(topic10, Encoding.UTF8.GetBytes(obsrValue10), MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE, false);
                                break;    
                            case "VVV":
                                string obsrValue11 = item.ToString();
                                string topic11 = "PohangPG/34682a43-5ff2-4768-b50b-abfc2fc14729/VVVVF";
                                mqttclient.Publish(topic11, Encoding.UTF8.GetBytes(obsrValue11), MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE, false);
                                break;    
                            case "WAV":
                                string obsrValue12 = item.ToString();
                                string topic12 = "PohangPG/34682a43-5ff2-4768-b50b-abfc2fc14729/WAVVF";
                                mqttclient.Publish(topic12, Encoding.UTF8.GetBytes(obsrValue12), MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE, false);
                                break;    
                            case "VEC":
                                string obsrValue13 = item.ToString();
                                string topic13 = "PohangPG/34682a43-5ff2-4768-b50b-abfc2fc14729/VECVF";
                                mqttclient.Publish(topic13, Encoding.UTF8.GetBytes(obsrValue13), MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE, false);
                                break;    
                            case "WSD":
                                string obsrValue14 = item.ToString();
                                string topic14 = "PohangPG/34682a43-5ff2-4768-b50b-abfc2fc14729/WSDVF";
                                mqttclient.Publish(topic14, Encoding.UTF8.GetBytes(obsrValue14), MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE, false);
                                break;    
                            }
                    }
                }
            }
            else
            {                    
                Console.WriteLine("error: " + res.StatusCode);
                //mqttclient.Publish(topic, "fail", MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE, false);
            }
            //heartbeat Data publish
            //mqttclient.Publish(topic, Encoding.UTF8.GetBytes("heartbeat"), MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE, false);
        }
        catch (Exception e)
        {
            //throw;
            string obsrValue = "HeartBeat Data";                
            string topic = "PohangPG/34682a43-5ff2-4768-b50b-abfc2fc14729/heartbeat";
            mqttclient.Publish(topic, Encoding.UTF8.GetBytes(obsrValue), MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE, false);
            //Console.WriteLine("error" + e.Message);
        }
    }
}

    //최신 해양데이터 API tideObsRecent
static async Task Publishing_ocean(MqttClient mqttclient)
{
    using (HttpClient client = new HttpClient())
    {
        try{
            Console.WriteLine("suc");
            string url = "https://www.khoa.go.kr/api/oceangrid/tideObsRecent/search.do?ServiceKey=EDrcCIFdi7qEsdqj0mgrjQ==&ObsCode=DT_0091&ResultType=json";
            HttpResponseMessage res = await client.GetAsync(url);
            Console.WriteLine("suc2");
            if(res.IsSuccessStatusCode){
                string data = await res.Content.ReadAsStringAsync();
                    if (data != null)
                    {                
                        var parsedData = JsonConvert.DeserializeObject<dynamic>(data);                    
                        var datas = parsedData.result.data;
                        var metas = parsedData.result.meta;
                        JObject obj = new JObject();
                        obj["data"] = datas;
                        obj["meta"] = metas;
                        string topic = "PohangPG/e4137834-cbcc-4241-b55a-aad674347287/ocean";
                        mqttclient.Publish(topic, Encoding.UTF8.GetBytes(obj.ToString()), MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE, false);
                        // string record = (string)items["record_time"];

                    }
            }
            else{
                Console.WriteLine("error: " + res.StatusCode);
            }
        } catch(Exception e){
            Console.WriteLine("Error: " + e.Message);
            if (e.InnerException != null)
            {
                Console.WriteLine("Inner Exception: " + e.InnerException.Message);
            }
        }
    }
}
//해양 기온,기압,풍향/풍속,염분,수온, 조위실제/관측데이터 API
static async Task Publishing_oceandata(MqttClient mqttclient)
{
    string[] arr = {"tideCurPre","tideObsTemp","tideObsSalt","tideObsAirTemp","tideObsAirPres","tideObsWind"};
    string currentDay = DateTime.Now.ToString("yyyyMMdd");
    using (HttpClient client = new HttpClient())
    {
        try{
            for(int i = 0; i<arr.Length; i++){
                string url = "https://www.khoa.go.kr/api/oceangrid/"+arr[i]+"/search.do?ServiceKey=EDrcCIFdi7qEsdqj0mgrjQ==&ObsCode=DT_0091&Date="+currentDay+"&ResultType=json";
                string topic = "PohangPG/e4137834-cbcc-4241-b55a-aad674347287/" + arr[i];
                HttpResponseMessage res = await client.GetAsync(url);
                if(res.IsSuccessStatusCode){
                    string data = await res.Content.ReadAsStringAsync();
                        if (data != null)
                        {                                            
                            var parsedData = JsonConvert.DeserializeObject<dynamic>(data);                    
                            var datas = parsedData.result.data;
                            var metas = parsedData.result.meta;                            
                            JObject obj = new JObject();
                            obj["meta"] = metas;
                            JArray dataArr = datas;
                            for(int j = 0; j<dataArr.Count;j++){
                                obj["data"] = dataArr[j];
                                mqttclient.Publish(topic, Encoding.UTF8.GetBytes(obj.ToString()), MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE, false);
                                obj.Remove("data");
                            }
                        }
                }
                else{
                    Console.WriteLine("error: " + res.StatusCode);
                }
            }            
        } catch(Exception e){
            Console.WriteLine("Error: " + e.Message);
            if (e.InnerException != null)
            {
                Console.WriteLine("Inner Exception: " + e.InnerException.Message);
            }
        }
    }
}
//자외선 API
static async Task Publishing_UV(MqttClient mqttclient)
{
    using(HttpClient client = new HttpClient())
    {
        try{
            string currentDay = DateTime.Now.ToString("yyyyMMdd");
            string currentTime = DateTime.Now.ToString("HHmm");
            string dayTime = currentDay + currentTime.Substring(0,2);            
            string url = "https://apis.data.go.kr/1360000/LivingWthrIdxServiceV4/getUVIdxV4?serviceKey=ClSg8YETAqDKd35FklryZ%2BYPkYWY6Q24PTpGdagqszHjRSWzowl08EhLqpdZKJvBkIUv%2FmrEcPwIK3iV8I3QVQ%3D%3D&pageNo=1&numOfRows=10&dataType=JSON&areaNo=4711155000&time=" + dayTime;
            HttpResponseMessage res = await client.GetAsync(url);
            if(res.IsSuccessStatusCode){
                string data = await res.Content.ReadAsStringAsync();
                if(data != null){
                    var parsedData = JsonConvert.DeserializeObject<dynamic>(data);
                    string item = parsedData.response.body.items.item[0].ToString();
                    string topic = "PohangPG/34682a43-5ff2-4768-b50b-abfc2fc14729/uv";                    
                    Console.WriteLine("uv"+item);
                    mqttclient.Publish(topic, Encoding.UTF8.GetBytes(item), MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE, false);
                }
                else{
                    Console.WriteLine("error: " + res.StatusCode);
                }
            }
        } catch(Exception e){
            Console.WriteLine(e.Message);
        }
    }
}
//대기 API
static async Task Publishing_ATMO(MqttClient mqttclient)
{
    using(HttpClient client = new HttpClient())
    {
        try{
            string currentDay = DateTime.Now.ToString("yyyyMMdd");
            string currentTime = DateTime.Now.ToString("HHmm");
            string dayTime = currentDay + currentTime.Substring(0,2);            
            String url = "https://apis.data.go.kr/1360000/LivingWthrIdxServiceV4/getAirDiffusionIdxV4?serviceKey=ClSg8YETAqDKd35FklryZ%2BYPkYWY6Q24PTpGdagqszHjRSWzowl08EhLqpdZKJvBkIUv%2FmrEcPwIK3iV8I3QVQ%3D%3D&pageNo=10&numOfRows=10&dataType=JSON&areaNo=4711155000&time="+dayTime;
            HttpResponseMessage res = await client.GetAsync(url);
            if(res.IsSuccessStatusCode){
                string data = await res.Content.ReadAsStringAsync();
                if(data != null){
                    var parsedData = JsonConvert.DeserializeObject<dynamic>(data);
                    string item = parsedData.response.body.items.item[0].ToString();
                    string topic = "PohangPG/34682a43-5ff2-4768-b50b-abfc2fc14729/atmo";                    
                    Console.WriteLine("atmo"+item);
                    mqttclient.Publish(topic, Encoding.UTF8.GetBytes(item), MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE, false);
                }
                else{
                    Console.WriteLine("error: " + res.StatusCode);
                }
            }
        } catch(Exception e){
            Console.WriteLine(e.Message);
        }
    }
}

//파고 API
static async Task Publishing_WAVE(MqttClient mqttclient)
{
    using(HttpClient client = new HttpClient())
    {
        try{
            string currentDay = DateTime.Now.ToString("yyyyMMdd");
            string currentTime = DateTime.Now.ToString("HHmm");
            string dayTime = currentDay + currentTime.Substring(0,2);            
            String url = "https://apis.data.go.kr/1360000/BeachInfoservice/getWhBuoyBeach?serviceKey=ClSg8YETAqDKd35FklryZ%2BYPkYWY6Q24PTpGdagqszHjRSWzowl08EhLqpdZKJvBkIUv%2FmrEcPwIK3iV8I3QVQ%3D%3D&numOfRows=1&pageNo=10&dataType=JSON&beach_num=211&searchTime=" + dayTime;
            HttpResponseMessage res = await client.GetAsync(url);
            if(res.IsSuccessStatusCode){
                string data = await res.Content.ReadAsStringAsync();
                if(data != null){
                    var parsedData = JsonConvert.DeserializeObject<dynamic>(data);
                    string item = parsedData.response.body.items.item[0].ToString();
                    string topic = "PohangPG/e4137834-cbcc-4241-b55a-aad674347287/wave";
                    Console.WriteLine("wave"+item);
                    mqttclient.Publish(topic, Encoding.UTF8.GetBytes(item), MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE, false);
                }
                else{
                    Console.WriteLine("error: " + res.StatusCode);
                }
            }
        } catch(Exception e){
            Console.WriteLine(e.Message);
        }
    }
}

//기상 특보 API


static async Task getWthrWrnMsg(MqttClient mqttclient){

    using(HttpClient client = new HttpClient())
    {
        try{
            string currentDay = DateTime.Now.ToString("yyyyMMdd");     
            string url = "https://apis.data.go.kr/1360000/WthrWrnInfoService/getWthrWrnMsg?serviceKey=ClSg8YETAqDKd35FklryZ%2BYPkYWY6Q24PTpGdagqszHjRSWzowl08EhLqpdZKJvBkIUv%2FmrEcPwIK3iV8I3QVQ%3D%3D&pageNo=1&numOfRows=100&dataType=json&stnId=143&fromTmFc="+currentDay+"&toTmFc="+currentDay;
            HttpResponseMessage res = await client.GetAsync(url);
            if(res.IsSuccessStatusCode){
                string data = await res.Content.ReadAsStringAsync();
                if(data != null){
                    var parsedData = JsonConvert.DeserializeObject<dynamic>(data);
                    JArray arr = parsedData.response.body.items.item;
                    for(int i = 0; i<arr.Count; i++){
                        string item = arr[i].ToString();
                        string topic = "PohangPG/34682a43-5ff2-4768-b50b-abfc2fc14729/WthrWrnMsg";
                        Console.WriteLine("WthrWrnMsg"+item);
                        mqttclient.Publish(topic, Encoding.UTF8.GetBytes(item), MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE, false);
                    }                    
                }
                else{
                    Console.WriteLine("error: " + res.StatusCode);
                }
            }
        } catch(Exception e){
            Console.WriteLine(e.Message);
        }
    }
}

static async Task gettideObsPreTab(MqttClient mqttclient){

    using(HttpClient client = new HttpClient())
    {
        try{            
            string currentDay = DateTime.Now.ToString("yyyyMMdd");     
            string url = "https://www.khoa.go.kr/api/oceangrid/tideObsPreTab/search.do?ServiceKey=EDrcCIFdi7qEsdqj0mgrjQ==&ObsCode=DT_0901&Date="+currentDay+"&ResultType=json";
            Console.WriteLine("error");
            HttpResponseMessage res = await client.GetAsync(url);
            Console.WriteLine(url);
            if(res.IsSuccessStatusCode){
                string data = await res.Content.ReadAsStringAsync();
                Console.WriteLine(data);
                if(data != null){
                    var parsedData = JsonConvert.DeserializeObject<dynamic>(data);
                    JArray arr = parsedData.response.body.items.item;
                    var datas = parsedData.result.data;
                    var metas = parsedData.result.meta;    
                    string topic = "PohangPG/" + uID + "/PreTab";                        
                    JObject obj = new JObject();
                    obj["meta"] = metas;
                    JArray dataArr = datas;
                    for(int j = 0; j<dataArr.Count;j++){
                        obj["data"] = dataArr[j];
                        mqttclient.Publish(topic, Encoding.UTF8.GetBytes(obj.ToString()), MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE, false);
                        obj.Remove("data");
                        }                                                            
                }
                else{
                    Console.WriteLine("error: " + res.StatusCode);
                }
            }
        } catch(Exception e){
            Console.WriteLine(e.Message);
        }
    }
}

static async Task getLunPhInfo(MqttClient mqttclient){

    using(HttpClient client = new HttpClient())
    {
        try{
            string currentDay = DateTime.Now.ToString("yyyyMMdd");
            string Y = currentDay.Substring(0,4);
            string M = currentDay.Substring(4,2);
            string D = currentDay.Substring(6);

            string url = "https://apis.data.go.kr/B090041/openapi/service/LunPhInfoService/getLunPhInfo?serviceKey=ClSg8YETAqDKd35FklryZ%2BYPkYWY6Q24PTpGdagqszHjRSWzowl08EhLqpdZKJvBkIUv%2FmrEcPwIK3iV8I3QVQ%3D%3D&solYear="+Y+"&solMonth="+M+"&solDay="+D;
            HttpResponseMessage res = await client.GetAsync(url);
            if(res.IsSuccessStatusCode){
                string data = await res.Content.ReadAsStringAsync();
                Console.WriteLine(data);
                if(data != null){
                    XmlDocument doc = new XmlDocument();
                    doc.LoadXml(data);
                    string json = JsonConvert.SerializeXmlNode(doc);
                    var parsedData = JsonConvert.DeserializeObject<dynamic>(json);
                    JObject obj = parsedData.response.body.items.item;
                    Console.WriteLine(obj.ToString());
                    string topic = "PohangPG/" + uID + "/LunPhInfo";
                    mqttclient.Publish(topic, Encoding.UTF8.GetBytes(obj.ToString()), MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE, false);                    
                }
                else{
                    Console.WriteLine("error: " + res.StatusCode);
                }
            }
        } catch(Exception e){
            Console.WriteLine(e.Message);
        }
    }
}


}
