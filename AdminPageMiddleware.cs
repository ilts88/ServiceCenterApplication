using Database;
using StringUtil;

public class AdminPageMiddleware
{
    RequestDelegate next;

    public AdminPageMiddleware(RequestDelegate next)
    {
        this.next = next;
    }

    public async Task InvokeAsync(HttpContext context,IConfiguration appConfiguration)
    {
        string? server = appConfiguration["server"];
        string? user = appConfiguration["user"];
        string? password = appConfiguration["password"];                    //получение из файла конфигурации данных для подключения к базе данных
        string? database = appConfiguration["database"];

        if (server != null && user != null && password != null && database != null)
        {
            MySqlDatabase ServiceCenterDatabase = new MySqlDatabase(server, user, password, database);             //создание объекта класса базы данных для дальнейшего взаимодействия
            if (context.Request.Path == "/adminpage/newspage")
            {
                await context.Response.SendFileAsync("adminnewspage.html");                                       //отправка страницы управления новостями
            }
            else if (context.Request.Path == "/adminpage/getrequests" && context.Request.Method == "GET")
            {
                string? order_by = null;
                string? select = null;
                if (context.Request.Query.ContainsKey("order_by"))                                              //если серверу был послан запрос на получение всех запросов на ремонт, производится попытка получить из параметров строки запроса выбранные пользователем параметры сортировки и отображения запросов
                    order_by = context.Request.Query["order_by"];
                if (context.Request.Query.ContainsKey("select"))
                    select = context.Request.Query["select"];
                if ((order_by == "ASC" || order_by == "DESC") && (select == "all" || select == "processing" || select == "appointed" || select == "rejected" || select == "resolved"))
                {
                    List<Request> requests;                       //если полученные параметры сортировки и отображения запросов на ремонт были корректны, сервер отправляет пользователю все запросы, соответствующие выбранным параметрам
                    if (select == "all")
                    {
                        requests = ServiceCenterDatabase.GetAllRequests(order_by);
                    }
                    else
                    {
                        requests = ServiceCenterDatabase.GetRequestsByStatus(order_by, select);
                    }
                    await context.Response.WriteAsJsonAsync(requests);
                }
                else                                     //если полученные параметры не прошли проверку, сервер отправляет ответ со статусным кодом ошибки и соответствующим сообщением
                {
                    context.Response.StatusCode = 400;
                    await context.Response.WriteAsJsonAsync("Запрос содержит некорректные данные");
                }
            }
            else if (context.Request.Path == "/adminpage/getrequestbyid" && context.Request.Method == "GET")
            {
                string? id = null;
                int int_id = 0;
                bool IsNumber = false;

                if (context.Request.Query.ContainsKey("id"))               //если серверу был послан запрос на получение по id запроса на ремонт, производится попытка извлечь из параметров строки запроса id запроса на ремонт
                    id = context.Request.Query["id"];
                if (id != null)
                    IsNumber = int.TryParse(id, out int_id);
                if (IsNumber)                                              //если id запроса на ремонт был успешно получен и преобразован к типу int, производится поиск запроса с полученным id в базе данных
                {
                    Request? request = ServiceCenterDatabase.GetRequestById(int_id);
                    if (request != null)
                    {
                        await context.Response.WriteAsJsonAsync(request);              //если запрос был найден, сервер отправляет его пользователю
                    }
                    else                                                        //если запрос не удалось найти, а также если переданный id не удалось успешно преобразовать к типу int, сервер отправляет ответ с соответствующим статусным кодом и сообщением об ошибке
                    {
                        context.Response.StatusCode = 400;                             
                        await context.Response.WriteAsJsonAsync("Запрос не найден");
                    }
                }
                else
                {
                    context.Response.StatusCode = 400;
                    await context.Response.WriteAsJsonAsync("переданный ID не является числом");
                }

            }
            else if (context.Request.Path == "/adminpage/updaterequest" && context.Request.Method == "PUT")
            {
                Request? request;
                try
                {
                    request = await context.Request.ReadFromJsonAsync<Request>();         //если серверу был послан запрос на обновление данных запроса на ремонт, то производится попытка чтения запроса на ремонт из объекта формата JSON
                }
                catch
                {
                    request = null;
                }
                if (request != null && request.service_comment != null)                      //производится проверка успешности чтения данных запроса
                {
                    bool IsValidStatus = StringValidator.IsValidStatus(request.status);                                     //производится проверка корректности обновляемых данных, статуса запроса и комментария сервисного центра
                    bool IsValidService_comment = StringValidator.IsValidService_comment(request.service_comment);
                    if (IsValidStatus && IsValidService_comment)
                    {
                        ServiceCenterDatabase.UpdateRequest(request.id, request.status, request.service_comment);           //если обновляемые данные прошли проверку, в базу данных вносятся произведённые изменения
                        await context.Response.WriteAsJsonAsync(request);                                                   //сервер посылает пользователю обновлённый запрос на ремонт
                    }
                    else                                                                                                //если обновляемые данные не прошли проверку, а также если запрос на ремонт не был получен, сервер отправляет ответ с соответствующим статусным кодом и сообщением об ошибке
                    {
                        context.Response.StatusCode = 400;
                        await context.Response.WriteAsJsonAsync("Недопустимый формат введённых данных");
                    }

                }
                else
                {
                    context.Response.StatusCode = 400;
                    await context.Response.WriteAsJsonAsync("Данные не были получены");
                }
            }
            else if (context.Request.Path == "/adminpage/deleterequest" && context.Request.Method == "DELETE")
            {
                string? id = null;
                int int_id = 0;
                bool IsNumber = false;

                if (context.Request.Query.ContainsKey("id"))                       //если серверу был послан запрос на удаление по id запроса на ремонт, производится попытка извлечь из параметров строки запроса id запроса на ремонт
                    id = context.Request.Query["id"];
                if (id != null)
                    IsNumber = int.TryParse(id, out int_id);
                if (IsNumber)                                                      //если id запроса на ремонт был успешно получен и преобразован к типу int, производится попытка удаления указанного запроса из базы данных
                {
                    int result_id = ServiceCenterDatabase.DeleteOldRequestById(int_id);
                    if (result_id != 0)                                                      //если запрос с указанным id был успешно удалён, сервер возвращает пользователю id удалённого запроса
                        await context.Response.WriteAsJsonAsync(result_id);
                    else                                                                   //если из-за проверок соответствия критериям удаления запрос не был удалён, а также если переданный id не удалось корректно преобразовать к типу int, сервер отправляет ответ с соответствующим статусным кодом и сообщением об ошибке
                    {
                        context.Response.StatusCode = 400;
                        await context.Response.WriteAsJsonAsync("Запрос не может быть удалён");
                    }
                }
                else
                {
                    context.Response.StatusCode = 400;
                    await context.Response.WriteAsJsonAsync("переданный ID не является числом");
                }
            }
            else if (context.Request.Path == "/adminpage/deletealloldrequests" && context.Request.Method == "DELETE")
            {
                List<int> deletedIds = ServiceCenterDatabase.DeleteAllOldRequests();                       //если серверу был послан запрос на удаление всех запросов на ремонт, соответствующих критериям удаления, сервер производит удаление всех подходящих запросов и возвращает пользователю массив, содержащий id всех удалённых запросов
                await context.Response.WriteAsJsonAsync(deletedIds);
            }
            else if (context.Request.Path == "/adminpage/createarticle" && context.Request.Method == "POST")
            {
                Article? result;
                try
                {
                    result = await context.Request.ReadFromJsonAsync<Article>();            //если сервер получил запрос на создание новой публикации, то производится попытка чтения публикации из объекта формата JSON
                }
                catch
                {
                    result = null;
                }
                if (result != null)                    //производится проверка успешности чтения данных публикации
                {
                    bool IsValidTitle = StringValidator.IsValidArticleTitle(result.title);            //производится проверка соответствия ожидаемому формату заголовка и содержания публикации
                    bool IsValidContent = StringValidator.IsValidArticleContent(result.content);
                    if (IsValidTitle && IsValidContent)                                              //если публикация прошла проверки, производится попытка создания публикации
                    {
                        int id = ServiceCenterDatabase.GetNewsMaximalId();                       //получение текущего наибольшего id публикаций
                        if (id < int.MaxValue)                                                   //проверка на доступность чисел в системе для создания новых id
                        {
                            id++;                                                                //если в системе доступны числа для создания id, новый id создаётся путём прибавления единицы к текущему наибольшему id
                            Article newarticle = new Article(id, result.title, result.content, result.created_at);
                            ServiceCenterDatabase.CreateArticle(newarticle);
                            Article? response_article = ServiceCenterDatabase.GetArticleById(id);
                            await context.Response.WriteAsJsonAsync(response_article);                 //в базе данных создаётся новая публикация, сервер возвращает пользователю данные созданной публикации
                        }
                        else                                     //если в системе недостаточно чисел для создания новых id, а также если данные не были получены или не прошли проверку на соответствие формату, сервер отправляет ответ с соответствующим статусным кодом и сообщением об ошибке
                        {
                            context.Response.StatusCode = 503;
                            await context.Response.WriteAsJsonAsync("База данных переполнена");
                        }
                    }
                    else
                    {
                        context.Response.StatusCode = 400;
                        await context.Response.WriteAsJsonAsync("Недопустимый формат введённых данных");
                    }

                }
                else
                {
                    context.Response.StatusCode = 400;
                    await context.Response.WriteAsJsonAsync("Данные не были получены");
                }
            }
            else if (context.Request.Path == "/adminpage/getallnews" && context.Request.Method == "GET")
            {
                string? order_by = null;
                if (context.Request.Query.ContainsKey("order_by"))      //если серверу был послан запрос на получение всех публикаций, производится попытка получить из параметров строки запроса выбранные пользователем параметры сортировки публикаций
                    order_by = context.Request.Query["order_by"];
                if (order_by == "ASC" || order_by == "DESC")
                {
                    List<Article> news = ServiceCenterDatabase.GetAllNews(order_by);       //если полученные параметры сортировки публикаций были корректны, сервер отправляет пользователю все публикации, применив выбранные параметры сортировки
                    await context.Response.WriteAsJsonAsync(news);
                }
                else                                                                  //если полученные параметры не прошли проверку, сервер отправляет ответ со статусным кодом ошибки и соответствующим сообщением
                {
                    context.Response.StatusCode = 400;
                    await context.Response.WriteAsJsonAsync("Запрос содержит некорректные данные");
                }
            }
            else if (context.Request.Path == "/adminpage/getarticlebyid" && context.Request.Method == "GET")
            {
                string? id = null;
                int int_id = 0;
                bool IsNumber = false;

                if (context.Request.Query.ContainsKey("id"))                        //если серверу был послан запрос на получение по id публикации, производится попытка извлечь из параметров строки запроса id публикации
                    id = context.Request.Query["id"];
                if (id != null)
                    IsNumber = int.TryParse(id, out int_id);
                if (IsNumber)                                              //если id публикации был успешно получен и преобразован к типу int, производится поиск публикации с полученным id в базе данных
                {
                    Article? article = ServiceCenterDatabase.GetArticleById(int_id);
                    if (article != null)
                    {
                        await context.Response.WriteAsJsonAsync(article);           //если публикация была найдена, сервер отправляет её пользователю
                    }
                    else                                                            //если запрос не удалось найти, а также если переданный id не удалось успешно преобразовать к типу int, сервер отправляет ответ с соответствующим статусным кодом и сообщением об ошибке
                    {
                        context.Response.StatusCode = 400;
                        await context.Response.WriteAsJsonAsync("Статья не найдена");
                    }
                }
                else
                {
                    context.Response.StatusCode = 400;
                    await context.Response.WriteAsJsonAsync("переданный ID не является числом");
                }

            }
            else if (context.Request.Path == "/adminpage/updatearticle" && context.Request.Method == "PUT")
            {
                Article? article;
                try
                {
                    article = await context.Request.ReadFromJsonAsync<Article>();            //если серверу был послан запрос на обновление данных публикации, то производится попытка чтения публикации из объекта формата JSON
                }
                catch
                {
                    article = null;
                }
                if (article != null)                                                            //производится проверка успешности чтения данных публикации
                {
                    bool IsValidTitle = StringValidator.IsValidArticleTitle(article.title);            //производится проверка соответствия ожидаемому формату заголовка и содержания публикации
                    bool IsValidContent = StringValidator.IsValidArticleContent(article.content);
                    if (IsValidTitle && IsValidContent)
                    {
                        ServiceCenterDatabase.UpdateArticle(article.id, article.title, article.content);             //если обновляемые данные прошли проверку, в базу данных вносятся произведённые изменения
                        await context.Response.WriteAsJsonAsync(article);                                            //сервер посылает пользователю обновлённую публикацию
                    }
                    else                                                                                     //если обновляемые данные не прошли проверку, а также если запрос на ремонт не был получен, сервер отправляет ответ с соответствующим статусным кодом и сообщением об ошибке
                    {
                        context.Response.StatusCode = 400;
                        await context.Response.WriteAsJsonAsync("Недопустимый формат введённых данных");
                    }

                }
                else
                {
                    context.Response.StatusCode = 400;
                    await context.Response.WriteAsJsonAsync("Данные не были получены");
                }
            }
            else if (context.Request.Path == "/adminpage/deletearticle" && context.Request.Method == "DELETE")
            {
                string? id = null;
                int int_id = 0;
                bool IsNumber = false;

                if (context.Request.Query.ContainsKey("id"))           //если серверу был послан запрос на удаление по id публикации, производится попытка извлечь из параметров строки запроса id публикации
                    id = context.Request.Query["id"];
                if (id != null)
                    IsNumber = int.TryParse(id, out int_id);
                if (IsNumber)                                          //если id публикации был успешно получен и преобразован к типу int, производится удаление указанной публикации из базы данных
                {
                    ServiceCenterDatabase.DeleteArticleById(int_id);
                    await context.Response.WriteAsJsonAsync(int_id);
                }
                else                                                   //если переданный id не удалось корректно преобразовать к типу int, сервер отправляет ответ со статусным кодом ошибки и соответствующим сообщением
                {
                    context.Response.StatusCode = 400;
                    await context.Response.WriteAsJsonAsync("переданный ID не является числом");
                }
            }
            else if (context.Request.Path == "/adminpage/admin_frame.css")
            {
                await context.Response.SendFileAsync("admin_frame.css");            //отправка файла макета страницы администратора
            }
            else
            {
                await context.Response.SendFileAsync("adminpage.html");             //отправка страницы администратора
            }
        }
        else                                                           //в случае, если из файла конфигурации не были получены данные для подключения к базе данных, сервер отправляет ответ со статусным кодом ошибки и соответствующим сообщением
        {
            context.Response.StatusCode = 500;
            await context.Response.WriteAsJsonAsync("Сервер недоступен, произошла критическая ошибка конфигурации");
        }
    }
}