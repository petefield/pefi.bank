using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Pefi.Bank.Shared.Commands;
using Pefi.Bank.Shared.ReadModels;

var apiBaseUrl = "http://localhost:5100";
var customerCount = 10;
var minAccounts = 2;
var maxAccounts = 4;

for (var i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--url" when i + 1 < args.Length:
            apiBaseUrl = args[++i];
            break;
        case "--customers" when i + 1 < args.Length:
            customerCount = int.Parse(args[++i]);
            break;
        case "--min-accounts" when i + 1 < args.Length:
            minAccounts = int.Parse(args[++i]);
            break;
        case "--max-accounts" when i + 1 < args.Length:
            maxAccounts = int.Parse(args[++i]);
            break;
        case "--help":
            Console.WriteLine("Pefi.Bank.Seeder - Creates test customers and accounts");
            Console.WriteLine();
            Console.WriteLine("Options:");
            Console.WriteLine("  --url <url>              API base URL (default: http://localhost:5100)");
            Console.WriteLine("  --customers <n>          Number of customers to create (default: 10)");
            Console.WriteLine("  --min-accounts <n>       Minimum accounts per customer (default: 2)");
            Console.WriteLine("  --max-accounts <n>       Maximum accounts per customer (default: 4)");
            Console.WriteLine("  --help                   Show this help message");
            return;
    }
}

if (minAccounts > maxAccounts)
{
    Console.Error.WriteLine("Error: --min-accounts cannot be greater than --max-accounts");
    return;
}

Console.WriteLine($"Pefi.Bank Seeder");
Console.WriteLine($"  API:          {apiBaseUrl}");
Console.WriteLine($"  Customers:    {customerCount}");
Console.WriteLine($"  Accounts:     {minAccounts}-{maxAccounts} per customer");
Console.WriteLine();

using var http = new HttpClient { BaseAddress = new Uri(apiBaseUrl) };

var random = new Random();
var totalAccounts = 0;
var totalDeposits = 0;

for (var i = 0; i < customerCount; i++)
{
    var firstName = FirstNames[random.Next(FirstNames.Length)];
    var lastName = LastNames[random.Next(LastNames.Length)];
    var uniqueSuffix = Guid.NewGuid().ToString("N")[..6];
    var email = $"{firstName.ToLowerInvariant()}.{lastName.ToLowerInvariant()}.{uniqueSuffix}@{EmailDomains[random.Next(EmailDomains.Length)]}";
    var password = "Password123!";

    var registerCmd = new RegisterCommand(firstName, lastName, email, password);
    var registerResponse = await http.PostAsJsonAsync("auth/register", registerCmd);

    if (!registerResponse.IsSuccessStatusCode)
    {
        Console.Error.WriteLine($"  Failed to register customer {firstName} {lastName}: {registerResponse.StatusCode}");
        continue;
    }

    var authResult = await registerResponse.Content.ReadFromJsonAsync<AuthResponse>();
    var customerId = authResult!.CustomerId;
    var token = authResult.Token;

    // Attach JWT for all subsequent calls for this customer
    http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

    Console.WriteLine($"[{i + 1}/{customerCount}] Registered customer: {firstName} {lastName} ({email}) - {customerId}");

    // Brief pause to let the projection catch up
    await Task.Delay(200);

    var accountCount = random.Next(minAccounts, maxAccounts + 1);

    for (var a = 0; a < accountCount; a++)
    {
        var accountName = PickAccountName(random, a);
        var accountCmd = new OpenAccountCommand(accountName);
        var accountResponse = await http.PostAsJsonAsync("accounts", accountCmd);

        if (!accountResponse.IsSuccessStatusCode)
        {
            Console.Error.WriteLine($"    Failed to open account '{accountName}': {accountResponse.StatusCode}");
            continue;
        }

        var accountResult = await accountResponse.Content.ReadFromJsonAsync<JsonElement>();
        var accountId = accountResult.GetProperty("id").GetGuid();
        totalAccounts++;

        Console.WriteLine($"    Account: {accountName} - {accountId}");

        // Brief pause, then deposit initial funds
        await Task.Delay(150);

        var depositAmount = GenerateDeposit(random, accountName);
        var depositCmd = new DepositCommand(depositAmount, "Initial deposit");
        var depositResponse = await http.PostAsJsonAsync($"accounts/{accountId}/deposit", depositCmd);

        if (depositResponse.IsSuccessStatusCode)
        {
            totalDeposits++;
            Console.WriteLine($"      Deposited {depositAmount:C}");
        }
        else
        {
            Console.Error.WriteLine($"      Failed to deposit: {depositResponse.StatusCode}");
        }
    }

    // Clear auth header before next customer registration
    http.DefaultRequestHeaders.Authorization = null;

    Console.WriteLine();
}

Console.WriteLine($"Seeding complete.");
Console.WriteLine($"  Customers created: {customerCount}");
Console.WriteLine($"  Accounts opened:   {totalAccounts}");
Console.WriteLine($"  Deposits made:     {totalDeposits}");

static string PickAccountName(Random random, int index)
{
    // First account is always a checking account, rest are random
    if (index == 0)
        return "Everyday Checking";

    return AccountNames[random.Next(AccountNames.Length)];
}

static decimal GenerateDeposit(Random random, string accountName)
{
    // Savings accounts get larger deposits
    if (accountName.Contains("Savings", StringComparison.OrdinalIgnoreCase))
        return Math.Round((decimal)(random.NextDouble() * 45000 + 5000), 2);

    if (accountName.Contains("Emergency", StringComparison.OrdinalIgnoreCase))
        return Math.Round((decimal)(random.NextDouble() * 15000 + 2000), 2);

    // Checking accounts get moderate deposits
    return Math.Round((decimal)(random.NextDouble() * 8000 + 500), 2);
}

partial class Program
{
    static readonly string[] FirstNames =
    [
        "James", "Mary", "Robert", "Patricia", "John", "Jennifer", "Michael", "Linda",
        "David", "Elizabeth", "William", "Barbara", "Richard", "Susan", "Joseph", "Jessica",
        "Thomas", "Sarah", "Charles", "Karen", "Christopher", "Lisa", "Daniel", "Nancy",
        "Matthew", "Betty", "Anthony", "Margaret", "Mark", "Sandra", "Donald", "Ashley",
        "Steven", "Dorothy", "Andrew", "Kimberly", "Paul", "Emily", "Joshua", "Donna",
        "Kenneth", "Michelle", "Kevin", "Carol", "Brian", "Amanda", "George", "Melissa",
        "Timothy", "Deborah", "Ronald", "Stephanie", "Edward", "Rebecca", "Jason", "Sharon",
        "Jeffrey", "Laura", "Ryan", "Cynthia", "Jacob", "Kathleen", "Gary", "Amy",
        "Nicholas", "Angela", "Eric", "Shirley", "Jonathan", "Anna", "Stephen", "Brenda",
        "Larry", "Pamela", "Justin", "Emma", "Scott", "Nicole", "Brandon", "Helen",
        "Benjamin", "Samantha", "Samuel", "Katherine", "Raymond", "Christine", "Gregory", "Debra",
        "Frank", "Rachel", "Alexander", "Carolyn", "Patrick", "Janet", "Jack", "Catherine"
    ];

    static readonly string[] LastNames =
    [
        "Smith", "Johnson", "Williams", "Brown", "Jones", "Garcia", "Miller", "Davis",
        "Rodriguez", "Martinez", "Hernandez", "Lopez", "Gonzalez", "Wilson", "Anderson", "Thomas",
        "Taylor", "Moore", "Jackson", "Martin", "Lee", "Perez", "Thompson", "White",
        "Harris", "Sanchez", "Clark", "Ramirez", "Lewis", "Robinson", "Walker", "Young",
        "Allen", "King", "Wright", "Scott", "Torres", "Nguyen", "Hill", "Flores",
        "Green", "Adams", "Nelson", "Baker", "Hall", "Rivera", "Campbell", "Mitchell",
        "Carter", "Roberts", "Gomez", "Phillips", "Evans", "Turner", "Diaz", "Parker",
        "Cruz", "Edwards", "Collins", "Reyes", "Stewart", "Morris", "Morales", "Murphy",
        "Cook", "Rogers", "Gutierrez", "Ortiz", "Morgan", "Cooper", "Peterson", "Bailey",
        "Reed", "Kelly", "Howard", "Ramos", "Kim", "Cox", "Ward", "Richardson",
        "Watson", "Brooks", "Chavez", "Wood", "James", "Bennett", "Gray", "Mendoza",
        "Ruiz", "Hughes", "Price", "Alvarez", "Castillo", "Sanders", "Patel", "Myers"
    ];

    static readonly string[] EmailDomains =
    [
        "gmail.com", "outlook.com", "yahoo.com", "icloud.com", "hotmail.com",
        "protonmail.com", "aol.com", "mail.com", "fastmail.com", "zoho.com"
    ];

    static readonly string[] AccountNames =
    [
        "Primary Savings", "Emergency Fund", "Vacation Savings", "Holiday Savings",
        "Joint Checking", "Business Checking", "Travel Fund",
        "Home Down Payment", "Car Fund", "Education Savings",
        "Everyday Savings", "Rainy Day Fund", "Investment Account"
    ];
}
