using Microsoft.EntityFrameworkCore;

namespace Database
{
     public class MySqlDatabase
    {
        protected string configuration;
        public MySqlDatabase(string server,string user,string password,string database)
        {
            configuration = "server=" + server + ";user=" + user + ";password=" + password + ";database=" + database + ";";   //создание строки конфигурации, используемой для создания объекта класса MySqlDatabaseContext
        }
        public int GetRequestsMaximalId()          //получение наибольшего id запросов на ремонт
        {
            int id = 0;                                                                //если в базе данных нет ни одного запроса на ремонт, метод возвращает 0
            using (MySqlDatabaseContext db = new MySqlDatabaseContext(configuration))
            {
                var result = db.Database.SqlQueryRaw<int?>("SELECT MAX(id) FROM requests");
                if (result.ToList()[0] != null)
                    id = result.ToList()[0].Value;
            }
            return id;
        }

        public void CreateRequest(Request request)   //создание запроса на ремонт
        {
            using (MySqlDatabaseContext db=new MySqlDatabaseContext(configuration))
            {
                
                db.Database.ExecuteSqlRaw("INSERT requests(id,fullname,phone,email,model,serial_number,purchase_date,address,malfunction,created_at,status,service_comment) VALUE ({0},{1},{2},{3},{4},{5},{6},{7},{8},NOW(),{9},{10})",request.id,request.fullname,request.phone,request.email,request.model,request.serial_number,request.purchase_date,request.address,request.malfunction,request.status,request.service_comment);
            }
        }

        public Request? GetRequestById(int id)      //получение по id запроса на ремонт
        {
            Request? request = null;
            using (MySqlDatabaseContext db=new MySqlDatabaseContext(configuration))
            {
                var result=db.requests.FromSqlRaw("SELECT * FROM requests WHERE id={0}",id);
                
                
                if (result.ToList().Count>0)
                    if (result.ToList()[0] != null)
                        request = result.ToList()[0];
                
                
            }
            return request;
        }
        public List<Request> GetAllRequests(string order_by)         //получение всех запросов на ремонт (запросы сортируются в соответствии с параметром сортировки)
        {
            List<Request> requests = null!;
            using (MySqlDatabaseContext db=new MySqlDatabaseContext(configuration))
            {
                requests=db.requests.FromSqlRaw("SELECT * FROM requests ORDER BY id " + order_by).ToList();
            }

            return requests;
        }
        public List<Request>GetRequestsByStatus(string order_by,string status)      //получение всех запросов на ремонт с указанным статусом (запросы сортируются в соответствии с параметром сортировки)
        {
            List<Request> requests = null!;
            using(MySqlDatabaseContext db=new MySqlDatabaseContext(configuration))
            {
                requests = db.requests.FromSqlRaw("SELECT * FROM requests WHERE status='" + status + "' ORDER BY id " + order_by).ToList();
            }

            return requests;
        }

        public void UpdateRequest(int id,string status,string service_comment)     //обновление статуса и комментария сервисного центра у запроса на ремонт с указанным id
        {
            using (MySqlDatabaseContext db=new MySqlDatabaseContext(configuration))
            {
                db.Database.ExecuteSqlRaw("UPDATE requests SET status={0},service_comment={1} WHERE id={2}",status,service_comment,id);
            }
        }
        public int DeleteOldRequestById(int id)           //удаление запроса на ремонт с указанным id
        {
            int result_id = 0;
            using (MySqlDatabaseContext db=new MySqlDatabaseContext(configuration))
            {
                string? status = null;                    //перед удалением для проверки соответствия критериям удаления производится получение из базы данных и оценка статуса и времени с момента создания запроса с указанным id
                int? time_passed = null;
                var status_response = db.Database.SqlQueryRaw<string?>("SELECT status FROM requests WHERE id={0}",id);
                if (status_response.ToList().Count > 0)
                    status = status_response.ToList()[0];
                var time_passed_response = db.Database.SqlQueryRaw<int?>("SELECT DATEDIFF(NOW(),created_at) FROM requests WHERE id={0}",id);     //для рассчёта времени с момента создания запроса используется функция DATEDIFF() в MySQL
                if (time_passed_response.ToList().Count > 0)
                    time_passed = time_passed_response.ToList()[0];
                if (status != null &&  time_passed != null && (status=="resolved" || status=="rejected") && time_passed > 180)    //если статус и время с момента создания соответствуют критериям удаления, производится удаление запроса
                {
                    db.Database.ExecuteSqlRaw("DELETE FROM requests WHERE id={0}",id);
                    result_id = id;
                }                                                                              //если удаление было произведено, метод возвращает id удалённого запроса, если удаление не было произведено, возвращается 0
            }
            return result_id;
        }

        public List<int> DeleteAllOldRequests()                   //удаление всех запросов на ремонт, соответствующих критериям удаления
        {
            List<int> deletedIds = new List<int>();
            using (MySqlDatabaseContext db=new MySqlDatabaseContext(configuration))
            {
                List<int?> IdForDeleting = db.Database.SqlQueryRaw<int?>("SELECT id FROM requests WHERE (DATEDIFF(NOW(),created_at)>180) AND (status='resolved' OR status='rejected')").ToList();         //получение из базы данных списка id всех запросов, соответствующих критериям удаления
                foreach (int? id in IdForDeleting)
                {
                    if (id != null)
                    {
                        int deleted_id = DeleteOldRequestById(id.Value);             //вызов метода удаления запроса по id для каждого id из полученного списка
                        deletedIds.Add(deleted_id);
                    }
                    
                }
            }
            return deletedIds;                       //метод возвращает список id всех удалённых запросов
        }

        public int GetNewsMaximalId()            //получение наибольшего id публикаций
        {
            int id = 0;                                                                  //если в базе данных нет ни одной публикации, метод возвращает 0
            using (MySqlDatabaseContext db = new MySqlDatabaseContext(configuration))
            {
                var result = db.Database.SqlQueryRaw<int?>("SELECT MAX(id) FROM news");
                if (result.ToList()[0] != null)
                    id = result.ToList()[0].Value;
            }
            return id;
        }

        public void CreateArticle(Article article)        //создание публикации
        {
            using (MySqlDatabaseContext db = new MySqlDatabaseContext(configuration))
            {
                db.Database.ExecuteSqlRaw("INSERT news(id,title,content,created_at) VALUE ({0},{1},{2},NOW())",article.id,article.title,article.content);
            }
        }

        public List<Article> GetAllNews(string order_by)   //получение всех публикаций (публикации сортируются в соответствии с параметром сортировки)
        {
            List<Article> news = null!;
            using (MySqlDatabaseContext db = new MySqlDatabaseContext(configuration))
            {
                news = db.news.FromSqlRaw("SELECT * FROM news ORDER BY id " + order_by).ToList();
            }

            return news;
        }

        public Article? GetArticleById(int id)        //получение публикации по id
        {
            Article? article = null;
            using (MySqlDatabaseContext db = new MySqlDatabaseContext(configuration))
            {
                var result = db.news.FromSqlRaw("SELECT * FROM news WHERE id={0}",id);
                

                if (result.ToList().Count > 0)
                    if (result.ToList()[0] != null)
                        article = result.ToList()[0];


            }
            return article;
        }

        public void UpdateArticle(int id, string title, string content)            //обновление публикации с указанным id
        {
            using (MySqlDatabaseContext db = new MySqlDatabaseContext(configuration))
            {
                db.Database.ExecuteSqlRaw("UPDATE news SET title={0},content={1} WHERE id={2}",title,content,id);
            }
        }

        public void DeleteArticleById(int id)            //удаление публикации по id
        {
            
            using (MySqlDatabaseContext db = new MySqlDatabaseContext(configuration))
            {
                
                    db.Database.ExecuteSqlRaw("DELETE FROM news WHERE id={0}",id);
                   
            }
            
        }
    }
    public class MySqlDatabaseContext : DbContext
    {
        protected string mysqlconfiguration;
        public DbSet<Request> requests { get; set; } = null!;         //создание объекта таблицы запросов на ремонт в базе данных
        public DbSet<Article> news { get; set; } = null!;             //создание объекта таблицы публикаций в базе данных

        public MySqlDatabaseContext(string configuration)
        {
            mysqlconfiguration = configuration;                    //получение данных конфигурации базы данных
            Database.EnsureCreated();                              //создание базы данных в случае её отсутствия
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)           //установление подключения к базе данных
        {
            optionsBuilder.UseMySql(mysqlconfiguration, new MySqlServerVersion(new Version(8, 0, 31)));
        }

    }

    public record Request(int id,string fullname,string phone,string email,string model,string serial_number,DateOnly purchase_date,string address,string malfunction,DateTime created_at,string status,string? service_comment);     //создание типа данных, характеризующего запрос на ремонт
    public record Article(int id,string title, string content,DateTime created_at);               //создание типа данных, характеризующего публикацию
}