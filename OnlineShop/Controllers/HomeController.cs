using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Npgsql;
using OnlineShop.Models;
using System.Collections.Generic;

public class HomeController : Controller
{
    private readonly IConfiguration _config;

    public HomeController(IConfiguration config)
    {
        _config = config;
    }

    public IActionResult Index()
    {
        var connectionString = _config.GetConnectionString("DefaultConnection");

        using var conn = new NpgsqlConnection(connectionString);
        conn.Open();

        // Получаем список товаров
        using var cmd = new NpgsqlCommand("SELECT id, name, description, price FROM products", conn);
        using var reader = cmd.ExecuteReader();

        List<Product> products = new List<Product>();
        while (reader.Read())
        {
            products.Add(new Product
            {
                Id = reader.GetInt32(0),
                Name = reader.GetString(1),
                Description = reader.GetString(2),
                Price = reader.GetDecimal(3)
            });
        }

        ViewBag.Products = products;

        reader.Close(); // Закрываем reader перед следующим SQL-запросом

        // Получаем баланс пользователя (user_id = 1)
        using var balanceCmd = new NpgsqlCommand("SELECT balance FROM users WHERE id = 1", conn);
        object balanceResult = balanceCmd.ExecuteScalar();
        decimal balance = balanceResult != null ? (decimal)balanceResult : 0;

        ViewBag.Balance = balance;

        return View();
    }
}
