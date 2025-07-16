using Newtonsoft.Json;
using System.Net.Http.Headers;
using System.Text;

namespace OnlineStoreClient
{
    class Program
    {
        private static readonly HttpClient client = new HttpClient();
        private static string token = string.Empty;
        private static readonly string baseUrl = "http://localhost:5112/api/";

        static async Task Main(string[] args)
        {
            client.BaseAddress = new Uri(baseUrl);
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            while (true)
            {
                Console.Clear();
                Console.WriteLine("=== Do4a Lab ===");
                Console.WriteLine("1. Вход");
                Console.WriteLine("2. Просмотреть товаров");
                Console.WriteLine("3. Добавить товар (Admin)");
                Console.WriteLine("4. Формирование заказа");
                Console.WriteLine("5. Просмотр заказов");
                Console.WriteLine("6. Изменение заказ");
                Console.WriteLine("7. Удалить заказ");
                Console.WriteLine("8. Поиск заказов по ID пользователя");
                Console.WriteLine("9. Обновление статуса заказа"); // Задание 4
                Console.WriteLine("10. Добавить товар в корзину"); // Задание 3
                Console.WriteLine("11. Выход");
                Console.Write("Выберите действие: ");

                var choice = Console.ReadLine();
                switch (choice)
                {
                    case "1": await Login(); break;
                    case "2": await ViewProducts(); break;
                    case "3": await AddProduct(); break;
                    case "4": await CreateOrder(); break;
                    case "5": await ViewOrders(); break;
                    case "6": await EditOrder(); break;
                    case "7": await DeleteOrder(); break;
                    case "8": await SearchOrders(); break;
                    case "9": await UpdateOrderStatus(); break; // Новый метод для Задания 4
                    case "10": await AddToCart(); break; // Новый метод для Задания 3
                    case "11": return;
                    default: Console.WriteLine("Неверный выбор. Нажмите Enter."); Console.ReadLine(); break;
                }
            }
        }

        static async Task Login()
        {
            Console.Write("Введите имя пользователя: ");
            var username = Console.ReadLine();
            Console.Write("Введите пароль: ");
            var password = Console.ReadLine();

            var content = new StringContent(JsonConvert.SerializeObject(new { Username = username, Password = password }), Encoding.UTF8, "application/json");
            Console.WriteLine($"[DEBUG] Отправка запроса на: {client.BaseAddress}users/login");
            var response = await client.PostAsync("users/login", content);
            Console.WriteLine($"[DEBUG] Статус ответа: {response.StatusCode} ({(int)response.StatusCode})");
            string responseContent = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"[DEBUG] Содержимое ответа: {responseContent}");

            if (response.IsSuccessStatusCode)
            {
                token = responseContent.Trim('"');
                Console.WriteLine($"Токен: {token}");
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                Console.WriteLine("Вход успешен! Нажмите Enter.");
            }
            else
            {
                Console.WriteLine("Ошибка входа: " + responseContent);
            }
            Console.ReadLine();
        }

        static async Task ViewProducts()
        {
            var response = await client.GetAsync("products");
            if (response.IsSuccessStatusCode)
            {
                var products = JsonConvert.DeserializeObject<List<Product>>(await response.Content.ReadAsStringAsync());
                foreach (var p in products)
                    Console.WriteLine($"{p.Id}. {p.Name} - {p.Price} ₽ - {p.Description}");
            }
            Console.WriteLine("Нажмите Enter для возврата.");
            Console.ReadLine();
        }

        static async Task AddProduct()
        {
            if (string.IsNullOrEmpty(token))
            {
                Console.WriteLine("[DEBUG] Токен отсутствует. Сначала войдите в систему как Admin.");
                Console.ReadLine();
                return;
            }

            Console.WriteLine($"[DEBUG] Используемый токен: {token}");
            Console.WriteLine($"[DEBUG] Заголовок Authorization: {client.DefaultRequestHeaders.Authorization?.ToString() ?? "Not set"}");
            Console.WriteLine($"[DEBUG] Отправка запроса на: {client.BaseAddress}products");

            Console.Write("Название товара: ");
            var name = Console.ReadLine();
            Console.Write("Цена: ");
            var priceInput = Console.ReadLine();
            if (!decimal.TryParse(priceInput, out decimal price))
            {
                Console.WriteLine("Ошибка: Введите корректную цену.");
                Console.ReadLine();
                return;
            }
            Console.Write("Описание: ");
            var description = Console.ReadLine();

            var content = new StringContent(JsonConvert.SerializeObject(new { Name = name, Price = price, Description = description }), Encoding.UTF8, "application/json");
            var response = await client.PostAsync("products", content);

            Console.WriteLine($"[DEBUG] Статус ответа: {response.StatusCode} ({(int)response.StatusCode})");
            string responseContent = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"[DEBUG] Содержимое ответа: {responseContent}");

            if (response.IsSuccessStatusCode)
                Console.WriteLine("Товар добавлен!");
            else
                Console.WriteLine("Ошибка: " + responseContent);
            Console.ReadLine();
        }

        static async Task CreateOrder()
        {
            if (string.IsNullOrEmpty(token)) { Console.WriteLine("Требуется вход."); Console.ReadLine(); return; }

            Console.Write("ID пользователя: ");
            var userId = int.Parse(Console.ReadLine() ?? "0");
            Console.Write("Введите ID товаров через запятую: ");
            var productIdsInput = Console.ReadLine();
            var productIds = string.IsNullOrWhiteSpace(productIdsInput)
                ? new List<int>()
                : productIdsInput.Split(',').Select(int.Parse).ToList();

            if (!productIds.Any())
            {
                Console.WriteLine("Ошибка: Необходимо указать хотя бы один товар.");
                Console.ReadLine();
                return;
            }

            var order = new
            {
                UserId = userId,
                Products = productIds.Select(id => new { Id = id }).ToList()
            };

            Console.WriteLine($"[DEBUG] Отправка запроса на: {client.BaseAddress}orders");
            Console.WriteLine($"[DEBUG] Тело запроса: {JsonConvert.SerializeObject(order)}");

            var response = await client.PostAsync("orders", new StringContent(JsonConvert.SerializeObject(order), Encoding.UTF8, "application/json"));

            Console.WriteLine($"[DEBUG] Статус ответа: {response.StatusCode} ({(int)response.StatusCode})");
            string responseContent = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"[DEBUG] Содержимое ответа: {responseContent}");

            if (response.IsSuccessStatusCode)
                Console.WriteLine("Заказ успешно создан!");
            else
                Console.WriteLine("Ошибка: " + responseContent);
            Console.ReadLine();
        }

        static async Task ViewOrders()
        {
            if (string.IsNullOrEmpty(token)) { Console.WriteLine("Требуется вход."); Console.ReadLine(); return; }

            var response = await client.GetAsync("orders");
            if (response.IsSuccessStatusCode)
            {
                var orders = JsonConvert.DeserializeObject<List<Order>>(await response.Content.ReadAsStringAsync());
                foreach (var o in orders)
                {
                    var productNames = string.Join(", ", o.Products.Select(p => p.Name));
                    Console.WriteLine($"{o.Id}. User ID: {o.UserId}, Total: {o.TotalPrice} ₽, Date: {o.OrderDate}, Status: {o.Status}, Products: {productNames}");
                }
            }
            else
            {
                Console.WriteLine("Ошибка загрузки заказов: " + await response.Content.ReadAsStringAsync());
            }
            Console.WriteLine("Нажмите Enter для возврата.");
            Console.ReadLine();
        }

        static async Task EditOrder()
        {
            if (string.IsNullOrEmpty(token)) { Console.WriteLine("Требуется вход."); Console.ReadLine(); return; }

            Console.Write("Введите ID заказа для редактирования: ");
            var orderId = int.Parse(Console.ReadLine() ?? "0");
            Console.Write("Новый ID пользователя: ");
            var userId = int.Parse(Console.ReadLine() ?? "0");
            Console.Write("Введите новые ID товаров через запятую (например, 1,2): ");
            var productIdsInput = Console.ReadLine();
            var productIds = string.IsNullOrWhiteSpace(productIdsInput)
                ? new List<int>()
                : productIdsInput.Split(',').Select(int.Parse).ToList();

            if (!productIds.Any())
            {
                Console.WriteLine("Ошибка: Необходимо указать хотя бы один товар.");
                Console.ReadLine();
                return;
            }

            var order = new
            {
                Id = orderId,
                UserId = userId,
                Products = productIds.Select(id => new { Id = id }).ToList()
            };

            Console.WriteLine($"[DEBUG] Отправка запроса на: {client.BaseAddress}orders/{orderId}");
            Console.WriteLine($"[DEBUG] Тело запроса: {JsonConvert.SerializeObject(order)}");

            var response = await client.PutAsync($"orders/{orderId}", new StringContent(JsonConvert.SerializeObject(order), Encoding.UTF8, "application/json"));

            Console.WriteLine($"[DEBUG] Статус ответа: {response.StatusCode} ({(int)response.StatusCode})");
            string responseContent = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"[DEBUG] Содержимое ответа: {responseContent}");

            if (response.IsSuccessStatusCode)
                Console.WriteLine("Заказ успешно обновлен!");
            else
                Console.WriteLine("Ошибка: " + responseContent);
            Console.ReadLine();
        }

        static async Task DeleteOrder()
        {
            if (string.IsNullOrEmpty(token)) { Console.WriteLine("Требуется вход."); Console.ReadLine(); return; }

            Console.Write("Введите ID заказа для удаления: ");
            var orderId = int.Parse(Console.ReadLine() ?? "0");

            Console.WriteLine($"[DEBUG] Отправка запроса на: {client.BaseAddress}orders/{orderId}");
            var response = await client.DeleteAsync($"orders/{orderId}");

            Console.WriteLine($"[DEBUG] Статус ответа: {response.StatusCode} ({(int)response.StatusCode})");
            string responseContent = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"[DEBUG] Содержимое ответа: {responseContent}");

            if (response.IsSuccessStatusCode)
                Console.WriteLine("Заказ успешно удален!");
            else
                Console.WriteLine("Ошибка: " + responseContent);
            Console.ReadLine();
        }

        static async Task SearchOrders()
        {
            if (string.IsNullOrEmpty(token)) { Console.WriteLine("Требуется вход."); Console.ReadLine(); return; }

            Console.Write("Введите ID пользователя для поиска заказов: ");
            var userId = int.Parse(Console.ReadLine() ?? "0");

            var response = await client.GetAsync("orders");
            if (response.IsSuccessStatusCode)
            {
                var orders = JsonConvert.DeserializeObject<List<Order>>(await response.Content.ReadAsStringAsync());
                var filteredOrders = orders.Where(o => o.UserId == userId).ToList();
                if (filteredOrders.Any())
                {
                    foreach (var o in filteredOrders)
                    {
                        var productNames = string.Join(", ", o.Products.Select(p => p.Name));
                        Console.WriteLine($"{o.Id}. User ID: {o.UserId}, Total: {o.TotalPrice} ₽, Date: {o.OrderDate}, Status: {o.Status}, Products: {productNames}");
                    }
                }
                else
                {
                    Console.WriteLine("Заказы не найдены.");
                }
            }
            else
            {
                Console.WriteLine("Ошибка загрузки заказов: " + await response.Content.ReadAsStringAsync());
            }
            Console.WriteLine("Нажмите Enter для возврата.");
            Console.ReadLine();
        }

        // Новый метод для обновления статуса заказа (Задание 4)
        static async Task UpdateOrderStatus()
        {
            if (string.IsNullOrEmpty(token)) { Console.WriteLine("Требуется вход."); Console.ReadLine(); return; }

            Console.Write("Введите ID заказа: ");
            var orderId = int.Parse(Console.ReadLine() ?? "0");
            Console.Write("Введите новый статус (например, Paid, Shipped): ");
            var status = Console.ReadLine();

            var update = new { Status = status };

            Console.WriteLine($"[DEBUG] Отправка запроса на: {client.BaseAddress}orders/{orderId}/status");
            Console.WriteLine($"[DEBUG] Тело запроса: {JsonConvert.SerializeObject(update)}");

            var response = await client.PutAsync($"orders/{orderId}/status", new StringContent(JsonConvert.SerializeObject(update), Encoding.UTF8, "application/json"));

            Console.WriteLine($"[DEBUG] Статус ответа: {response.StatusCode} ({(int)response.StatusCode})");
            string responseContent = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"[DEBUG] Содержимое ответа: {responseContent}");

            if (response.IsSuccessStatusCode)
                Console.WriteLine("Статус заказа успешно обновлен!");
            else
                Console.WriteLine("Ошибка: " + responseContent);
            Console.ReadLine();
        }

        // Новый метод для добавления товара в корзину (Задание 3)
        static async Task AddToCart()
        {
            if (string.IsNullOrEmpty(token)) { Console.WriteLine("Требуется вход."); Console.ReadLine(); return; }

            Console.Write("Введите ID пользователя: ");
            var userId = int.Parse(Console.ReadLine() ?? "0");
            Console.Write("Введите ID товара: ");
            var productId = int.Parse(Console.ReadLine() ?? "0");

            var cartItem = new { UserId = userId, ProductId = productId };

            Console.WriteLine($"[DEBUG] Отправка запроса на: {client.BaseAddress}cart/add");
            Console.WriteLine($"[DEBUG] Тело запроса: {JsonConvert.SerializeObject(cartItem)}");

            var response = await client.PostAsync("cart/add", new StringContent(JsonConvert.SerializeObject(cartItem), Encoding.UTF8, "application/json"));

            Console.WriteLine($"[DEBUG] Статус ответа: {response.StatusCode} ({(int)response.StatusCode})");
            string responseContent = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"[DEBUG] Содержимое ответа: {responseContent}");

            if (response.IsSuccessStatusCode)
                Console.WriteLine("Товар добавлен в корзину!");
            else
                Console.WriteLine("Ошибка: " + responseContent);
            Console.ReadLine();
        }
    }

    public class Product
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public string Description { get; set; } = string.Empty;
    }

    public class Order
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public List<Product> Products { get; set; } = new();
        public decimal TotalPrice { get; set; }
        public DateTime OrderDate { get; set; }
        public string Status { get; set; } = "Pending"; // Добавлено для отображения статуса
    }
}