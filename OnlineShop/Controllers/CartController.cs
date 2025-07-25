using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Npgsql;
using System.Data;

public class CartController : Controller
{
    private readonly IConfiguration _config;

    public CartController(IConfiguration config)
    {
        _config = config;
    }

    public IActionResult AddToCart(int id)
    {
        var connectionString = _config.GetConnectionString("DefaultConnection");
        using var conn = new NpgsqlConnection(connectionString);
        conn.Open();

        decimal price = 0;

        // Получаем цену товара
        using (var cmd = new NpgsqlCommand("SELECT price FROM products WHERE id = @id", conn))
        {
            cmd.Parameters.AddWithValue("id", id);
            object result = cmd.ExecuteScalar();

            if (result == null)
            {
                return Content("Товар не найден.");
            }

            price = (decimal)result;
        }

        // Создаём заказ, но НЕ списываем средства и не помечаем как оплаченный
        int orderId;
        using (var cmd = new NpgsqlCommand("INSERT INTO orders (user_id, total, is_paid) VALUES (1, @total, false) RETURNING id", conn))
        {
            cmd.Parameters.AddWithValue("total", price);
            orderId = (int)cmd.ExecuteScalar();
        }

        using (var cmd = new NpgsqlCommand("INSERT INTO order_items (order_id, product_id, quantity) VALUES (@oid, @pid, 1)", conn))
        {
            cmd.Parameters.AddWithValue("oid", orderId);
            cmd.Parameters.AddWithValue("pid", id);
            cmd.ExecuteNonQuery();
        }

        return RedirectToAction("Payment", new { orderId = orderId });
    }

    public IActionResult Payment(int orderId)
    {
        var connectionString = _config.GetConnectionString("DefaultConnection");
        using var conn = new NpgsqlConnection(connectionString);
        conn.Open();

        decimal amount = 0;

        using (var cmd = new NpgsqlCommand("SELECT total FROM orders WHERE id = @orderId AND user_id = 1", conn))
        {
            cmd.Parameters.AddWithValue("orderId", orderId);
            var result = cmd.ExecuteScalar();

            if (result == null)
                return Content("Заказ не найден");

            amount = (decimal)result;
        }

        ViewBag.Amount = amount;
        ViewBag.OrderId = orderId;

        return View();
    }


    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult PaymentSuccess(int orderId)
    {
        var connectionString = _config.GetConnectionString("DefaultConnection");
        using var conn = new NpgsqlConnection(connectionString);
        conn.Open();

        using var transaction = conn.BeginTransaction();

        try
        {
            // Получаем заказ
            decimal amount = 0;
            int userId = 1; // В идеале брать из контекста аутентификации

            using (var cmd = new NpgsqlCommand("SELECT total FROM orders WHERE id = @orderId AND user_id = @userId AND is_paid = false", conn, transaction))
            {
                cmd.Parameters.AddWithValue("orderId", orderId);
                cmd.Parameters.AddWithValue("userId", userId);

                var result = cmd.ExecuteScalar();

                if (result == null)
                {
                    return Content("Заказ не найден или уже оплачен.");
                }

                amount = (decimal)result;
            }

            // Получаем баланс
            decimal balance = 0;
            using (var cmd = new NpgsqlCommand("SELECT balance FROM users WHERE id = @userId", conn, transaction))
            {
                cmd.Parameters.AddWithValue("userId", userId);
                object result = cmd.ExecuteScalar();
                if (result == null)
                    return Content("Пользователь не найден.");

                balance = (decimal)result;
            }

            if (balance < amount)
            {
                transaction.Rollback();
                return RedirectToAction("InsufficientFunds");
            }

            // Списываем баланс
            using (var cmd = new NpgsqlCommand("UPDATE users SET balance = balance - @amount WHERE id = @userId", conn, transaction))
            {
                cmd.Parameters.AddWithValue("amount", amount);
                cmd.Parameters.AddWithValue("userId", userId);
                cmd.ExecuteNonQuery();
            }

            // Обновляем заказ как оплаченный
            using (var cmd = new NpgsqlCommand("UPDATE orders SET is_paid = true WHERE id = @orderId", conn, transaction))
            {
                cmd.Parameters.AddWithValue("orderId", orderId);
                cmd.ExecuteNonQuery();
            }

            transaction.Commit();
            return RedirectToAction("ThankYou");
        }
        catch
        {
            transaction.Rollback();
            return Content("Ошибка при оплате.");
        }
    }


    public IActionResult ThankYou()
    {
        return View();
    }
    public IActionResult InsufficientFunds()
    {
        return View();
    }

}
