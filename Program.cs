using System.Diagnostics;
using System.Globalization;
using CsvHelper;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;

var inputs = Environment.GetCommandLineArgs().Skip(1);
if (!inputs.Any())
{
    Console.WriteLine("Pass the path of the input file!");
    Environment.Exit(0);
}

var filePath = args[0];
// var batchSize = int.Parse(args[1]);
// var filePath = "Input.txt";
var batchSize = 100;

var chasis = File.ReadAllLines(Path.Combine(Directory.GetCurrentDirectory(), filePath));

var chromeOptions = new ChromeOptions();
chromeOptions.AddArgument("--headless");
chromeOptions.AddArguments("window-size=1200,1100");

// chasis = chasis.Take(100).ToArray();
var allChunks = chasis.Chunk(batchSize);

var chromeDriver = new ChromeDriver(chromeOptions);
chromeDriver.Url = "https://tgtransport.net/TGCFSTONLINE/Reports/VehicleRegistrationSearch.aspx";
var allTasks = new List<Task>();
for(var i = 0; i < allChunks.Count(); i++)
{
    // create all Tasks
    var chunk = allChunks.ElementAt(i);

    var dropdown = chromeDriver.FindElement(By.Name("ctl00$OnlineContent$ddlInput"));
    var selectElement = new SelectElement(dropdown);
    selectElement.SelectByValue("E");
    Thread.Sleep(500);
    
    var vehicleDetails = FetchVehicleDetailsList(chunk, chromeDriver, string.Empty);
    WriteToCsv(vehicleDetails);
    // var taskName = "Task " + i;
    // var task =
    //     new Task(() =>
    //     {
    //         var chromeDriver = new ChromeDriver(chromeOptions);
    //         chromeDriver.Url = "https://tgtransport.net/TGCFSTONLINE/Reports/VehicleRegistrationSearch.aspx";
    //
    //         var dropdown = chromeDriver.FindElement(By.Name("ctl00$OnlineContent$ddlInput"));
    //         var selectElement = new SelectElement(dropdown);
    //         selectElement.SelectByValue("C");
    //         Thread.Sleep(200);
    //         
    //         var vehicleDetails = FetchVehicleDetailsList(chunk, chromeDriver, taskName);
    //         Console.WriteLine($"TaskName: {taskName}: Vehicle Details: {vehicleDetails.Count}");
    //         WriteToCsv(vehicleDetails);
    //     });
    // allTasks.Add(task);
}

// var watch = Stopwatch.StartNew();
// Parallel.ForEach(allTasks, task => task.Start());
// await Task.WhenAll(allTasks);
// watch.Stop();
// var elapsedMs = watch.ElapsedMilliseconds;
// Console.WriteLine($"Time Taken: {elapsedMs/1000} seconds");
// Console.WriteLine("Processes completed!");

void WriteToCsv(List<VehicleDetails> vehicleDetailsList)
{
    using (var writer = new StreamWriter(Environment.CurrentDirectory + $"/VehicleDetails_{DateTime.Now.ToString("MM-dd-yyyy-hh:mm:ss")}.csv"))
    using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
    {
        csv.WriteRecords(vehicleDetailsList);
    }
}

List<VehicleDetails> FetchVehicleDetailsList(string[] strings, IWebDriver webDriver, string taskName)
{
    var list = new List<VehicleDetails>();
    for (var i = 0; i < strings.Length; i++)
    {
        try
        {
            var details = FetchVehicleDetails(strings[i]);
            list.Add(details);
            Console.WriteLine($"Task {taskName}: #{i}/{strings.Length}, {details}");
        }
        catch (Exception ex)
        {
            list.Add(new VehicleDetails()
            {
                ChasisNumber = strings[i],
                FinancierName = "EXCEPTION",
                NoDataFound = true,
                OwnerName = ex.Message,
            });
        }
    }

    return list;

    VehicleDetails FetchVehicleDetails(string ch)
    {
        // set the chasis
        var chasisItem = webDriver.FindElement(By.Name("ctl00$OnlineContent$txtInput"));
        chasisItem?.Clear();
        chasisItem?.SendKeys(ch);
    
        var captcha = webDriver.FindElement(By.Id("ctl00_OnlineContent_imgCaptcha"));
        var captchaValue = captcha?.GetAttribute("src");
        captchaValue = captchaValue.Replace("https://tgtransport.net/TGCFSTONLINE/Captcha/CaptchaHandler.ashx?query=", "");
    
        var captchaElement = webDriver.FindElement(By.Id("ctl00_OnlineContent_txtCaptcha"));
        captchaElement?.Clear();
        captchaElement?.SendKeys(captchaValue);
    
        var getDataButton = webDriver.FindElement(By.Id("ctl00_OnlineContent_btnGetData"));
        getDataButton.Click();
    
        Thread.Sleep(500);

        if (webDriver.FindElement(By.Id("ctl00_OnlineContent_lblMsg")).Text == "No Data Found")
        {
            return new VehicleDetails
            {
                ChasisNumber = ch,
                NoDataFound = true
            };
        }
        // ctl00_OnlineContent_lblMsg
        var ownerName = webDriver.FindElement(By.Id("ctl00_OnlineContent_tdOwner")).Text;
        var financierName = webDriver.FindElement(By.Id("ctl00_OnlineContent_tdFin")).Text;
        var vd =  new VehicleDetails
        {
            ChasisNumber = ch,
            OwnerName = ownerName,
            FinancierName = financierName
        };
        return vd;
    }
}

class VehicleDetails
{
    public string ChasisNumber { get; set; }
    public string OwnerName { get; set; }
    public string FinancierName { get; set; }
    public bool NoDataFound { get; set; } = false;

    public override string ToString()
    {
        return $"Owner: {OwnerName}, Financier: {FinancierName}, Chasis: {ChasisNumber}";
    }
}

public static class WebDriverExtensions
{
    public static IWebElement FindElement(this IWebDriver driver, By by, int timeoutInSeconds)
    {
        if (timeoutInSeconds > 0)
        {
            var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(timeoutInSeconds));
            wait.IgnoreExceptionTypes();
            return wait.Until(drv => drv.FindElement(by));
        }
        return driver.FindElement(by);
    }
}