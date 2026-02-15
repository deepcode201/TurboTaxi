using System.Text;

namespace TurboTaxi.API.Middlewares
{
    public class LoggingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<LoggingMiddleware> _logger;
        private readonly IWebHostEnvironment _env;

        public LoggingMiddleware(RequestDelegate next, ILogger<LoggingMiddleware> logger, IWebHostEnvironment Env)
        {
            _next = next;
            _logger = logger;
            _env = Env;
        }

        public async Task Invoke(HttpContext context)
        {
            string clientIpAddress = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(clientIpAddress))
            {
                clientIpAddress = context.Connection.RemoteIpAddress.ToString();
            }

            DateTime currentDate = DateTime.Now;

            string logFileName = $"{currentDate:yyyy-MM}.log";

            string logFilePath;

            if (_env.WebRootPath.Contains("MVC"))
            {
                var path = _env.WebRootPath.Replace("UniversityLifeApp.MVC", "UniversityLifeApp.API");
                var path2 = path.Replace("universitylife-api", @"universitylife-api\src");
                logFilePath = Path.Combine(path2 + "\\ogging", logFileName);
            }

            else
            {
                logFilePath = Path.Combine(_env.WebRootPath + "\\logging", logFileName);
            }



            try
            {
                string requestType = context.Request.Method;
                string path = context.Request.Path;
                string requestBody = await GetRequestBody(context);





                StringBuilder stringBuilder = new StringBuilder();

                stringBuilder.Append("----------Request Started----------\n");
                stringBuilder.Append($"REQUEST DATE : {currentDate:yyyy-MM-dd HH:mm:ss}\n");
                stringBuilder.Append($"IP ADDRESS: {clientIpAddress}\n");
                stringBuilder.Append($"PATH : {path} \n");
                if (context.Request.Method == "POST" || context.Request.Method == "PUT" || context.Request.Method == "GET")
                {
                    //TODO: take body as json
                    stringBuilder.Append($"REQUEST BODY : {requestBody}\n");

                }


                File.AppendAllText(logFilePath, stringBuilder.ToString() + Environment.NewLine);
            }
            catch (Exception ex)
            {
                StringBuilder stringBuilder = new StringBuilder();

                stringBuilder.Append("----------Request Started----------\n");
                stringBuilder.Append($"REQUEST DATE : {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n");
                stringBuilder.Append($"IP ADDRESS: {clientIpAddress}\n");
                stringBuilder.Append($"PATH : {context.Request.Path} \n");
                stringBuilder.Append($"Error : {ex.Message}\n");

                _logger.LogError(stringBuilder.ToString());
                //TODO: return Internal Server Error;
            }

            await _next(context);


            StringBuilder responseStringBuilder = new StringBuilder();
            var responseBody = await GetResponseBody(context);
            if (context.Request.Method == "POST" || context.Request.Method == "PUT" || context.Request.Method == "GET")
            {
                responseStringBuilder.Append($"RESPONSE BODY : ${responseBody}\n");
            }
            responseStringBuilder.Append("----------Request Ended----------\n");
            File.AppendAllText(logFilePath, responseStringBuilder.ToString() + Environment.NewLine);

        }

        private async Task<string> GetRequestBody(HttpContext context)
        {
            context.Request.EnableBuffering();

            using (StreamReader reader = new StreamReader(context.Request.Body, Encoding.UTF8, true, 1024, true))
            {
                var body = await reader.ReadToEndAsync();
                context.Request.Body.Position = 0;

                return body;
            }
        }

        private async Task<string> GetResponseBody(HttpContext context)
        {
            context.Request.EnableBuffering();

            using (MemoryStream responseBodyStream = new MemoryStream())
            {
                responseBodyStream.Seek(0, SeekOrigin.Begin);
                using (StreamReader reader = new StreamReader(responseBodyStream, Encoding.UTF8, true, 1024, true))
                {
                    var body = await reader.ReadToEndAsync();
                    return body;
                }
            }

        }
    }
}
