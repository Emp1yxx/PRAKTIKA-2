#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

//  Исключение для ошибок предметной области (некорректные данные)
class DrinkException : Exception
{
    public DrinkException(string message) : base(message) { }
}

//  Структура «Объём» — неизменяемая, с проверкой диапазона
readonly struct Volume
{
    public const double Min = 0;
    public const double Max = 1000;

    public double Value { get; }

    public Volume(double value)
    {
        if (double.IsNaN(value) || value < Min || value > Max)
            throw new DrinkException($"Объём должен быть от {Min} до {Max} мл.");
        Value = value;
    }

    public override string ToString() => $"{Value:0.##} мл";
}

//  Справочник видов чая и кофе (пользователь может добавлять новые)
static class KindCatalog
{
    private const int MinLength = 2;
    private const int MaxLength = 30;

    // Буквы (любого алфавита), слова разделены пробелом или дефисом
    private static readonly Regex Allowed = new Regex(@"^\p{L}+([ \-]\p{L}+)*$");

    public static readonly List<string> Tea = new List<string>
        { "Черный", "Зеленый", "Травяной", "Улун", "Пуэр", "Каркаде" };

    public static readonly List<string> Coffee = new List<string>
        { "Эспрессо", "Американо", "Капучино", "Латте", "Раф" };

    /// <summary>Проверяет и приводит название к единому виду («пУэР» -> «Пуэр»).</summary>
    public static bool TryNormalize(string input, out string result, out string error)
    {
        result = "";
        string s = Regex.Replace(input.Trim(), @"\s+", " ");

        if (s.Length < MinLength || s.Length > MaxLength)
        {
            error = $"Длина названия должна быть от {MinLength} до {MaxLength} символов.";
            return false;
        }
        if (!Allowed.IsMatch(s))
        {
            error = "Допустимы только буквы, пробелы и дефисы (без цифр и спецсимволов).";
            return false;
        }

        result = char.ToUpper(s[0]) + s.Substring(1).ToLower();
        error = "";
        return true;
    }

    public static string? Find(List<string> list, string kind) =>
        list.FirstOrDefault(k => string.Equals(k, kind, StringComparison.CurrentCultureIgnoreCase));
}

//  Интерфейс «можно пить»
interface IDrinkable
{
    string DrinkActionName { get; }   // «Выпить» или «Допить»
    bool DrinkUp();                   // true — глоток сделан
}

//  Базовый класс Drink (разные уровни доступа)
class Drink
{
    // --- константы и поля только для чтения ---
    public const string CafeName = "Чайная";
    protected const double SipVolume = 50;          // protected: доступно наследникам
    private static int nextId = 1;                  // private: счётчик
    public readonly int Id;                         // readonly: назначается один раз

    // --- приватное поле + публичные свойства ---
    private string type = "";

    public Volume Volume { get; set; }

    public string Type
    {
        get => type;
        set => type = RequireText(value, "Тип напитка");
    }

    /// <summary>Если осталось меньше глотка — действие называется «Допить».</summary>
    public string DrinkActionName => Volume.Value < SipVolume ? "Допить" : "Выпить";

    public Drink(double volume, string type)
    {
        Id = nextId++;
        Volume = new Volume(volume);
        Type = type;
    }

    protected static string RequireText(string value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new DrinkException($"{fieldName} не может быть пустым.");
        value = value.Trim();
        if (value.Length > 80)
            throw new DrinkException($"{fieldName} слишком длинное (максимум 80 символов).");
        return value;
    }

    /// <summary>Общая логика глотка: 50 мл, а если осталось меньше — допивает остаток.</summary>
    protected bool TryDrink(bool isReady, string notReadyMessage, string title)
    {
        if (!isReady)
        {
            Console.WriteLine(notReadyMessage);
            return false;
        }

        double amount = Math.Min(SipVolume, Volume.Value);
        bool last = amount >= Volume.Value;              // последний глоток?
        Volume = new Volume(Volume.Value - amount);

        if (last)
            Console.WriteLine($"{title} допит ({amount:0.##} мл).");
        else
            Console.WriteLine($"{title} выпит ({amount:0.##} мл). Осталось: {Volume}");
        return true;
    }

    public override string ToString() => $"{type}, {Volume}";
}

//  Класс Tea
class Tea : Drink, IDrinkable
{
    public const int MaxBags = 20;

    private int quantity;
    private string teaKind = "";

    public int Quantity
    {
        get => quantity;
        set
        {
            if (value < 0 || value > MaxBags)
                throw new DrinkException($"Количество пакетиков должно быть от 0 до {MaxBags}.");
            quantity = value;
        }
    }

    public string TeaKind
    {
        get => teaKind;
        set => teaKind = RequireText(value, "Вид чая");
    }

    public bool IsBrewed { get; private set; }

    // --- конструкторы ---
    public Tea(double volume, string kind, int quantity) : base(volume, "Чай")
    {
        TeaKind = kind;
        Quantity = quantity;
    }

    // Конструктор копирования
    public Tea(Tea other) : base(other.Volume.Value, "Чай")
    {
        TeaKind = other.TeaKind;
        Quantity = other.Quantity;
        IsBrewed = other.IsBrewed;
    }

    // --- Метод 1: заварить ---
    public bool Brew()
    {
        if (IsBrewed)
        {
            Console.WriteLine("Чай уже заварен.");
            return false;
        }
        if (Quantity == 0)
        {
            Console.WriteLine("Нет пакетиков, заваривать нечего!");
            return false;
        }
        IsBrewed = true;
        Console.WriteLine($"Чай {TeaKind} заварен.");
        return true;
    }

    // --- Метод 2: выпить глоток (50 мл) или допить остаток ---
    public bool DrinkUp()
    {
        bool ok = TryDrink(IsBrewed, $"Чай {TeaKind} не заварен. Сначала заварите его.", $"Чай {TeaKind}");
        if (ok && Volume.Value == 0) IsBrewed = false;
        return ok;
    }

    // --- Метод 3: описание ---
    public string Describe() =>
        $"Чай {TeaKind}, объём {Volume}, пакетиков: {Quantity}, {(IsBrewed ? "заварен" : "не заварен")}";

    // --- Перегрузка арифметической операции (+ ; работает и как +=) ---
    public static Tea operator +(Tea a, Tea b)
    {
        bool sameKind = string.Equals(a.TeaKind, b.TeaKind, StringComparison.CurrentCultureIgnoreCase);
        string kind = sameKind ? a.TeaKind : $"{a.TeaKind} и {b.TeaKind}";

        var result = new Tea(a.Volume.Value + b.Volume.Value, kind, a.Quantity + b.Quantity);
        result.IsBrewed = a.IsBrewed && b.IsBrewed;
        return result;
    }

    // --- «Перегрузка присваивания» ---
    public void Assign(Tea other)
    {
        if (ReferenceEquals(this, other)) return;

        Volume = other.Volume;
        TeaKind = other.TeaKind;
        Quantity = other.Quantity;
        IsBrewed = other.IsBrewed;
    }

    public override string ToString() => Describe();
}

//  Класс Coffee
class Coffee : Drink, IDrinkable
{
    public const int MaxSugar = 5;

    private string coffeeKind = "";
    private int sugar;

    public string CoffeeKind
    {
        get => coffeeKind;
        set => coffeeKind = RequireText(value, "Вид кофе");
    }

    public int Sugar
    {
        get => sugar;
        set
        {
            if (value < 0 || value > MaxSugar)
                throw new DrinkException($"Ложек сахара должно быть от 0 до {MaxSugar}.");
            sugar = value;
        }
    }

    public bool WithMilk { get; set; }

    public bool IsBrewed { get; private set; }

    public Coffee(double volume, string kind, int sugar, bool withMilk) : base(volume, "Кофе")
    {
        CoffeeKind = kind;
        Sugar = sugar;
        WithMilk = withMilk;
    }

    // --- Метод 1: сварить ---
    public bool Brew()
    {
        if (IsBrewed)
        {
            Console.WriteLine("Кофе уже сварен.");
            return false;
        }
        IsBrewed = true;
        Console.WriteLine($"Кофе {CoffeeKind} сварен.");
        return true;
    }

    // --- Метод 2: выпить глоток (50 мл) или допить остаток ---
    public bool DrinkUp()
    {
        bool ok = TryDrink(IsBrewed, $"Кофе {CoffeeKind} не сварен. Сначала сварите его.", $"Кофе {CoffeeKind}");
        if (ok && Volume.Value == 0) IsBrewed = false;
        return ok;
    }

    // --- Метод 3: копирование параметров ---
    public void Assign(Coffee other)
    {
        if (ReferenceEquals(this, other)) return;

        Volume = other.Volume;
        CoffeeKind = other.CoffeeKind;
        Sugar = other.Sugar;
        WithMilk = other.WithMilk;
        IsBrewed = other.IsBrewed;
    }

    public override string ToString() =>
        $"Кофе {CoffeeKind}, объём {Volume}, сахар (ложек): {Sugar}, " +
        $"{(WithMilk ? "с молоком" : "без молока")}, {(IsBrewed ? "сварен" : "не сварен")}";
}

//  Интерфейс и класс Cafe
interface ICafe
{
    void ShowDrinks();
    void Add(Drink drink);
    bool Remove(Drink drink);
    void Clear();
}

class Cafe : ICafe
{
    public List<Drink> Drinks { get; } = new List<Drink>();

    public void Add(Drink drink) => Drinks.Add(drink);

    public bool Remove(Drink drink) => Drinks.Remove(drink);

    public void Clear() => Drinks.Clear();

    public void ShowDrinks()
    {
        Console.WriteLine($"\n=== {Drink.CafeName} ===");
        if (Drinks.Count == 0)
        {
            Console.WriteLine("(список пуст)");
            return;
        }
        for (int i = 0; i < Drinks.Count; i++)
            Console.WriteLine($"{i + 1}. [id {Drinks[i].Id}] {Drinks[i]}");
    }
}

//  Программа с меню
class Program
{
    //ввод с проверкой
    static string ReadRaw(string prompt)
    {
        Console.Write(prompt);
        string? s = Console.ReadLine();
        if (s == null) Environment.Exit(0);      // конец ввода (Ctrl+Z / Ctrl+D)
        return s!.Trim();
    }

    static int ReadInt(string prompt, int min, int max)
    {
        while (true)
        {
            string s = ReadRaw(prompt);
            if (int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out int r)
                && r >= min && r <= max)
                return r;
            Console.WriteLine($"Ошибка: введите целое число от {min} до {max}.");
        }
    }

    static double ReadDouble(string prompt, double min, double max)
    {
        while (true)
        {
            string s = ReadRaw(prompt).Replace(',', '.');   // принимаем и 200,5 и 200.5
            if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out double r)
                && r >= min && r <= max)
                return r;
            Console.WriteLine($"Ошибка: введите число от {min} до {max}.");
        }
    }

    static bool ReadYesNo(string prompt)
    {
        while (true)
        {
            string s = ReadRaw(prompt).ToLower();
            if (s is "д" or "да" or "y" or "yes") return true;
            if (s is "н" or "нет" or "n" or "no") return false;
            Console.WriteLine("Ошибка: ответьте «д» или «н».");
        }
    }

    /// <summary>Ввод нового вида. Пустая строка — отмена (вернёт null).</summary>
    static string? ReadNewKind(List<string> catalog)
    {
        while (true)
        {
            string s = ReadRaw("Название нового вида (Enter — отмена): ");
            if (s.Length == 0) return null;

            if (!KindCatalog.TryNormalize(s, out string kind, out string error))
            {
                Console.WriteLine("Ошибка: " + error);
                continue;
            }

            string? existing = KindCatalog.Find(catalog, kind);
            if (existing != null)
            {
                Console.WriteLine($"Такой вид уже есть в справочнике — выбран «{existing}».");
                return existing;
            }

            catalog.Add(kind);
            Console.WriteLine($"Новый вид «{kind}» добавлен в справочник.");
            return kind;
        }
    }

    /// <summary>Выбор вида из списка или ввод нового.</summary>
    static string ReadKind(List<string> catalog, string what)
    {
        while (true)
        {
            Console.WriteLine($"Виды ({what}):");
            for (int i = 0; i < catalog.Count; i++)
                Console.WriteLine($"  {i + 1}. {catalog[i]}");
            Console.WriteLine("  0. Ввести новый вид");

            int n = ReadInt("Выбор: ", 0, catalog.Count);
            if (n > 0) return catalog[n - 1];

            string? created = ReadNewKind(catalog);
            if (created != null) return created;
        }
    }

    static Tea ReadTea()
    {
        double v = ReadDouble($"Объём чая, мл (1–{Volume.Max}): ", 1, Volume.Max);
        string kind = ReadKind(KindCatalog.Tea, "чай");
        int q = ReadInt($"Пакетиков (0–{Tea.MaxBags}): ", 0, Tea.MaxBags);
        return new Tea(v, kind, q);
    }

    static Coffee ReadCoffee()
    {
        double v = ReadDouble($"Объём кофе, мл (1–{Volume.Max}): ", 1, Volume.Max);
        string kind = ReadKind(KindCatalog.Coffee, "кофе");
        int sugar = ReadInt($"Ложек сахара (0–{Coffee.MaxSugar}): ", 0, Coffee.MaxSugar);
        bool milk = ReadYesNo("С молоком? (д/н): ");
        return new Coffee(v, kind, sugar, milk);
    }

    /// <summary>Показывает напитки кафе нужного типа и просит выбрать один. null — отмена или список пуст.</summary>
    static T? Pick<T>(Cafe cafe, string question, bool showAction = false) where T : Drink
    {
        var items = cafe.Drinks.OfType<T>().ToList();
        if (items.Count == 0)
        {
            Console.WriteLine(cafe.Drinks.Count == 0
                ? "В кафе пока нет напитков. Добавьте их через пункт 1."
                : "В кафе нет подходящих напитков.");
            return null;
        }

        Console.WriteLine($"\n{question}");
        for (int i = 0; i < items.Count; i++)
        {
            string action = showAction && items[i] is IDrinkable dr
                ? $"   → {dr.DrinkActionName.ToLower()}"
                : "";
            Console.WriteLine($"{i + 1}. {items[i]}{action}");
        }

        int n = ReadInt("Номер (0 — отмена): ", 0, items.Count);
        return n == 0 ? null : items[n - 1];
    }

    static void PrintMenu()
    {
        Console.WriteLine("\n--- МЕНЮ ---");
        Console.WriteLine(" 1. Добавить напиток в кафе (задать параметры)");
        Console.WriteLine(" 2. Показать все напитки (свойства)");
        Console.WriteLine(" 3. Изменить параметры напитка");
        Console.WriteLine(" 4. Заварить чай / сварить кофе");
        Console.WriteLine(" 5. Выпить (выбрать, что пить)");
        Console.WriteLine(" 6. Сложить два чая (оператор +)");
        Console.WriteLine(" 7. Присвоить один чай другому (Assign)");
        Console.WriteLine(" 8. Удалить напиток из кафе");
        Console.WriteLine(" 9. Очистить кафе");
        Console.WriteLine("10. Справочник видов (показать / добавить новый вид)");
        Console.WriteLine(" 0. Выход");
    }

    static void Main()
    {
        Console.OutputEncoding = Encoding.UTF8;
        Console.InputEncoding = Encoding.UTF8;

        Cafe cafe = new Cafe();

        while (true)
        {
            PrintMenu();
            int choice = ReadInt("Выбор: ", 0, 10);

            try
            {
                switch (choice)
                {
                    case 1:
                    {
                        Console.WriteLine("\nЧто добавить в кафе?");
                        Console.WriteLine("1 - Чай");
                        Console.WriteLine("2 - Кофе");
                        Console.WriteLine("3 - Копию существующего чая");
                        Console.WriteLine("0 - Отмена");
                        int kind = ReadInt("Ваш выбор: ", 0, 3);

                        if (kind == 1)
                        {
                            cafe.Add(ReadTea());
                            Console.WriteLine("Чай добавлен в кафе.");
                        }
                        else if (kind == 2)
                        {
                            cafe.Add(ReadCoffee());
                            Console.WriteLine("Кофе добавлен в кафе.");
                        }
                        else if (kind == 3)
                        {
                            var src = Pick<Tea>(cafe, "Какой чай скопировать?");
                            if (src != null)
                            {
                                cafe.Add(new Tea(src));
                                Console.WriteLine("Копия чая добавлена в кафе.");
                            }
                        }
                        break;
                    }

                    case 2:
                        cafe.ShowDrinks();
                        break;

                    case 3:
                    {
                        var d = Pick<Drink>(cafe, "Параметры какого напитка изменить?");
                        if (d is Tea t)
                        {
                            t.Assign(ReadTea());
                            Console.WriteLine($"Обновлено: {t}");
                        }
                        else if (d is Coffee c)
                        {
                            c.Assign(ReadCoffee());
                            Console.WriteLine($"Обновлено: {c}");
                        }
                        break;
                    }

                    case 4:
                    {
                        var d = Pick<Drink>(cafe, "Что заварить / сварить?");
                        if (d is Tea t) t.Brew();
                        else if (d is Coffee c) c.Brew();
                        break;
                    }

                    case 5:
                    {
                        var d = Pick<Drink>(cafe, "Что выпить?", showAction: true);
                        if (d is IDrinkable drinkable && drinkable.DrinkUp() && d.Volume.Value == 0)
                        {
                            cafe.Remove(d);
                            Console.WriteLine("Напиток закончился и удалён из списка.");
                        }
                        break;
                    }

                    case 6:
                    {
                        var a = Pick<Tea>(cafe, "Первый чай для сложения:");
                        if (a == null) break;
                        var b = Pick<Tea>(cafe, "Второй чай для сложения:");
                        if (b == null) break;

                        Tea sum = a + b;
                        cafe.Add(sum);
                        Console.WriteLine($"Результат добавлен в кафе: {sum}");
                        break;
                    }

                    case 7:
                    {
                        var target = Pick<Tea>(cafe, "Какому чаю присвоить параметры (целевой)?");
                        if (target == null) break;
                        var source = Pick<Tea>(cafe, "Чьи параметры взять (исходный)?");
                        if (source == null) break;

                        if (ReferenceEquals(target, source))
                        {
                            Console.WriteLine("Выбран один и тот же чай — присваивать нечего.");
                            break;
                        }
                        target.Assign(source);
                        Console.WriteLine($"Теперь: {target}");
                        break;
                    }

                    case 8:
                    {
                        var d = Pick<Drink>(cafe, "Какой напиток удалить?");
                        if (d != null && cafe.Remove(d))
                            Console.WriteLine("Напиток удалён.");
                        break;
                    }

                    case 9:
                        if (cafe.Drinks.Count == 0)
                            Console.WriteLine("Кафе уже пусто.");
                        else if (ReadYesNo($"Удалить все напитки ({cafe.Drinks.Count} шт.)? (д/н): "))
                        {
                            cafe.Clear();
                            Console.WriteLine("Список кафе очищен.");
                        }
                        break;

                    case 10:
                    {
                        Console.WriteLine("\n1 - Показать справочник");
                        Console.WriteLine("2 - Добавить новый вид чая");
                        Console.WriteLine("3 - Добавить новый вид кофе");
                        Console.WriteLine("0 - Назад");
                        int sub = ReadInt("Выбор: ", 0, 3);

                        if (sub == 1)
                        {
                            Console.WriteLine("Чай:   " + string.Join(", ", KindCatalog.Tea));
                            Console.WriteLine("Кофе:  " + string.Join(", ", KindCatalog.Coffee));
                        }
                        else if (sub == 2) ReadNewKind(KindCatalog.Tea);
                        else if (sub == 3) ReadNewKind(KindCatalog.Coffee);
                        break;
                    }

                    case 0:
                        Console.WriteLine("До свидания!");
                        return;
                }
            }
            catch (DrinkException ex)
            {
                Console.WriteLine("Ошибка: " + ex.Message);
            }
        }
    }
}