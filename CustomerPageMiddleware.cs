using Database;
using StringUtil;
public class CustomerPageMiddleware
{
    RequestDelegate next;

    public CustomerPageMiddleware(RequestDelegate next)
    {
        this.next = next;
    }

    public async Task InvokeAsync(HttpContext context,IConfiguration appConfiguration)
    {
        string? server = appConfiguration["server"];         //получение из файла конфигурации данных для подключения к базе данных
        string? user = appConfiguration["user"];
        string? password = appConfiguration["password"];
        string? database = appConfiguration["database"];

        if (server != null && user != null && password != null && database != null)
        {
            MySqlDatabase ServiceCenterDatabase = new MySqlDatabase(server, user, password, database);      //создание объекта класса базы данных для дальнейшего взаимодействия
            if (context.Request.Path.StartsWithSegments("/adminpage"))
            {
                await next.Invoke(context);                                //в случае, если запрос относится к части системы, предназначенной для администратора, вызывается следующий middleware
            }
            else if (context.Request.Path == "/createrequest" && context.Request.Method == "POST")
            {
                Request? result;
                try
                {
                    result = await context.Request.ReadFromJsonAsync<Request>();       //если серверу был послан запрос на создание запроса на ремонт, то производится попытка чтения запроса на ремонт из объекта формата JSON
                }
                catch
                {
                    result = null;
                }
                if (result != null)                                         //производится проверка успешности чтения данных запроса
                {
                    int id = ServiceCenterDatabase.GetRequestsMaximalId();   //получение текущего наибольшего id запросов на ремонт
                    int year = 2000 + (id / 10000000);
                    int month = id % 10000000 / 100000;                      //в системе id всех запросов на ремонт содержат в начале дату создания запроса
                    if (month == 0)
                        month++;                                             //поэтому для того, чтобы оценить, какой id присвоить новому запросу, производится оценка даты создания наибольшего id запросов
                    int day = id % 100000 / 1000;
                    if (day == 0)                                            //в случае, когда ранее в системе не было ни одного запроса, наибольший id будет равен нулю
                        day++;                                               //поскольку конструктору объекта класса DateOnly можно передавать только значения месяца и дня, которые начинаются с единицы, в таком случае значения дня и месяца увеличиваются на единицу для дальнейшего корректного сравнения
                    DateOnly date_id = new DateOnly(year, month, day);
                    DateOnly current_date = new DateOnly(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day);
                    if (date_id < current_date)                                                                           //если наибольший id был создан ранее сегодняшнего дня, то новому запросу присваивается первый id, созданный на основе даты сегодняшнего дня 
                    {
                        id = (DateTime.Now.Year - 2000) * 10000000 + (DateTime.Now.Month) * 100000 + DateTime.Now.Day * 1000;
                    }
                    else                                                                                                  //иначе новому запросу присваевается наибольший id, увеличенный на единицу
                    {
                        id++;                                                                        
                    }
                    if (id % 1000 < 999)                       //в системе установлен суточный лимит на количество новых запросов
                    {
                        Request newrequest = new Request(id, result.fullname, result.phone, result.email, result.model, result.serial_number, result.purchase_date, result.address, result.malfunction, result.created_at, result.status, result.service_comment);
                        bool IsValidNewRequest = StringValidator.IsValidNewRequest(newrequest);
                        if (IsValidNewRequest)                                                         //производится проверка корректности содержания запроса на ремонт
                        {
                            ServiceCenterDatabase.CreateRequest(newrequest);                           //в случае успешного прохождения всех проверок, с помощью объекта класса базы данных создаётся новый запрос
                            await context.Response.WriteAsJsonAsync(newrequest);                       //сервер возвращает пользователю данные нового запроса
                        }
                        else                                                                           //в случае, если полученные данные не прошли одну из проверок, сервер отправляет ответ с соответствующим статусным кодом и сообщением об ошибке
                        {
                            
                            context.Response.StatusCode = 400;
                            await context.Response.WriteAsJsonAsync("Недопустимый формат введённых данных");
                        }

                    }
                    else
                    {
                        context.Response.StatusCode = 503;
                        await context.Response.WriteAsJsonAsync("Превышен суточный лимит запросов");
                    }
                }
                else
                {
                    context.Response.StatusCode = 400;
                    await context.Response.WriteAsJsonAsync("Данные не были получены");
                }
            }
            else if (context.Request.Path == "/requestpage")                   
            {
                await context.Response.SendFileAsync("requestpage.html");              //отправка страницы создания запроса на ремонт
            }
            else if (context.Request.Path == "/getrequeststatus" && context.Request.Method == "GET")
            {
                string? id = null;
                int int_id = 0;
                bool IsNumber = false;

                if (context.Request.Query.ContainsKey("id"))            //если серверу был послан запрос на получение статуса запроса на ремонт, производится попытка извлечь из параметров строки запроса id запроса на ремонт
                    id = context.Request.Query["id"];
                if (id != null)
                    IsNumber = int.TryParse(id, out int_id);
                if (IsNumber)                                           //если id запроса на ремонт был успешно получен и преобразован к типу int, производится поиск запроса с полученным id в базе данных
                {
                    Request? request = ServiceCenterDatabase.GetRequestById(int_id);
                    if (request != null)
                    {
                        var status_info = new { request.status, request.service_comment };               //если запрос с указанным id был найден, для предотвращения раскрытия персональных данных клиентов сервер создаёт на основе найденного запроса новый объект, содержащий информацию только о статусе запроса и комментарии сервисного центра, и отправляет его пользователю
                        await context.Response.WriteAsJsonAsync(status_info);
                    }
                    else
                    {
                        context.Response.StatusCode = 204;                 //если запрос не был найден, пользователю оптравляется статусный код, сообщающий об отсутствии результата
                    }

                }
                else                                                    //если переданный id не удалось корректно преобразовать к типу int, сервер отправляет ответ со статусным кодом ошибки и соответствующим сообщением
                {
                    context.Response.StatusCode = 400;
                    await context.Response.WriteAsJsonAsync("переданный ID не является числом");
                }



            }
            else if (context.Request.Path == "/getallnews" && context.Request.Method == "GET")
            {
                List<Article> news = ServiceCenterDatabase.GetAllNews("DESC");            //если серверу был послан запрос на получение всех новостей, он получает из базы данных все новости, сортированные по убыванию, и отправляет их пользователю
                await context.Response.WriteAsJsonAsync(news);
            }
            else if (context.Request.Path == "/statuspage")
            {
                await context.Response.SendFileAsync("statuspage.html");                  //отправка страницы проверки статуса запроса на ремонт
            }
            else if (context.Request.Path == "/newspage")
            {
                await context.Response.SendFileAsync("newspage.html");                    //отправка страницы новостей
            }
            else if (context.Request.Path == "/aboutpage")
            {
                await context.Response.SendFileAsync("aboutpage.html");                   //отправка страницы с информацией о компании
            }
            else if (context.Request.Path == "/LOGO.png")
            {
                await context.Response.SendFileAsync("LOGO.png");                         //отправка изображений для работы рекламы гарантии компании
            }
            else if (context.Request.Path == "/1.png")
            {
                await context.Response.SendFileAsync("1.png");
            }
            else if (context.Request.Path == "/2.png")
            {
                await context.Response.SendFileAsync("2.png");
            }
            else if (context.Request.Path == "/3.png")
            {
                await context.Response.SendFileAsync("3.png");
            }
            else if (context.Request.Path == "/4.png")
            {
                await context.Response.SendFileAsync("4.png");
            }
            else if (context.Request.Path == "/5.png")
            {
                await context.Response.SendFileAsync("5.png");
            }
            else if (context.Request.Path == "/6.png")
            {
                await context.Response.SendFileAsync("6.png");
            }
            else if (context.Request.Path == "/white.png")
            {
                await context.Response.SendFileAsync("white.png");
            }
            else if (context.Request.Path == "/frame.css")
            {
                await context.Response.SendFileAsync("frame.css");                               //отправка файла макета страницы
            }
            else if (context.Request.Path == "/warranty_promotion_animation.css")
            {
                await context.Response.SendFileAsync("warranty_promotion_animation.css");        //отправка файла анимации рекламы гарантии компании
            }
            else
            {
                await context.Response.SendFileAsync("mainpage.html");                           //отправка главной страницы
            }
        }
        else                                                                         //в случае, если из файла конфигурации не были получены данные для подключения к базе данных, сервер отправляет ответ со статусным кодом ошибки и соответствующим сообщением
        {
            context.Response.StatusCode = 500;
            await context.Response.WriteAsJsonAsync("Сервер недоступен, произошла критическая ошибка конфигурации");
        }
    }
}