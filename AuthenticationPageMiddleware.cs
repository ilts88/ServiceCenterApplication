using Microsoft.AspNetCore.Authentication;
using System.Security.Claims;

public class AuthenticationPageMiddleware
{
    RequestDelegate next;

    public AuthenticationPageMiddleware(RequestDelegate next)
    {
        this.next = next;
    }

    public async Task InvokeAsync(HttpContext context,IConfiguration appConfiguration)
    {
        string GeneratedKey = StringGenerator.GetGeneratedKey(8,16);           //получение сгенерированного ключа аутентификации
        string? AdminLogin = appConfiguration["adminlogin"];
        string? AdminPassword = appConfiguration["adminpassword"];             //получение из файла конфигурации логина и пароля администратора
        string? login = null;
        string? password = null;
        if (context.Request.HasFormContentType)
        {
            if (context.Request.Form.ContainsKey("login"))
                login = context.Request.Form["login"];
            if (context.Request.Form.ContainsKey("password"))                   //попытка получения из переданного запроса введённых логина и пароля
                password = context.Request.Form["password"];
        }
        var Identity = context.User.Identity;                                   //попытка получения данных аутентификации пользователя, включая текущий используемый ключ аутентификации
        var Key = context.User.FindFirst("key");
        if (AdminLogin != null && AdminPassword != null)                        //проверка успешности получения логина и пароля администратора из файлов конфигурации
        {
            if (Identity != null && Key != null && Identity.IsAuthenticated && Key.Value == GeneratedKey)
            {
                await next.Invoke(context);                             //если пользователь аутентифицирован и использует актуальный ключ аутентификации, ему открывается доступ к панели администратора и вызывается следующий middleware
            }
            else if (login != null && password != null)        //иначе, если пользователь ввёл логин и пароль, производится их проверка
            {
                if (login == AdminLogin && password == AdminPassword)      //если пользователь ввёл правильный логин и пароль, он аутентифицируется и ему присваевается актуальный ключ аутентификации
                {
                    var claims = new List<Claim> { new Claim("key", GeneratedKey) };
                    var claimsIdentity = new ClaimsIdentity(claims, "Cookies");
                    var claimsPrincipal = new ClaimsPrincipal(claimsIdentity);
                    await context.SignInAsync(claimsPrincipal);
                    await next.Invoke(context);                             //также пользователю сразу же открывается доступ к панели администратора и вызывается следующий middleware
                }
                else
                {
                    context.Response.StatusCode = 403;                 //если пользователь ввёл неправильные логин или пароль, сервер отправляет ответ со статусным кодом ошибки аутентификации
                }
            }
            else                                       //если пользователь не аутентифицирован с использованием актуального ключа аутентификации, и при этом он не вводил логин и пароль, то ему отправляется страница для ввода логина и пароля
            {
                if (Identity != null && Identity.IsAuthenticated)      //в таком случае, если ранее пользователь был аутентифицирован, данные его аутентификации удаляются
                    await context.SignOutAsync();
                await context.Response.SendFileAsync("authenticationpage.html");
            }
        }
        else                                          //если сервер не смог получить из файла конфигурации логин и пароль администратора, сервер отправляет ответ со статусным кодом ошибки и соответствующим сообщением
        {
            context.Response.StatusCode = 500;
            await context.Response.WriteAsJsonAsync("Сервер недоступен, произошла критическая ошибка конфигурации");
        }
    }
}