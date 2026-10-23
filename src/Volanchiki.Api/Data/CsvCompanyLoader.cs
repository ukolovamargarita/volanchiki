using System.Globalization;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;
using Volanchiki.Api.Domain;

namespace Volanchiki.Api.Data;

public class CsvCompanyLoader
{
    readonly ILogger<CsvCompanyLoader> _log;

    public CsvCompanyLoader(ILogger<CsvCompanyLoader> log) => _log = log;

    public async Task LoadIntoAsync(CompanyStore store, string dataDir, CancellationToken ct)
    {
        var companiesPath = Path.Combine(dataDir, "df_Uid_data_hack.csv");
        var peoplePath = Path.Combine(dataDir, "df_Fid_Uid_link_hack.csv");
        var paymentsPath = Path.Combine(dataDir, "df_Uid_payment_hack.csv");

        if (!File.Exists(companiesPath))
            throw new FileNotFoundException("Нет файла юрлиц", companiesPath);

        _log.LogInformation("Читаю компании из {Path}", companiesPath);
        var companies = await ReadCompaniesAsync(companiesPath, ct);

        var people = File.Exists(peoplePath)
            ? await ReadPeopleAsync(peoplePath, ct)
            : new List<PersonLink>();

        var payments = File.Exists(paymentsPath)
            ? await ReadPaymentsAsync(paymentsPath, ct)
            : new List<PaymentEdge>();

        store.ReplaceAll(companies, people, payments);
        _log.LogInformation(
            "Загружено: компаний {C}, связей ФЛ {P}, платежей {Pay}",
            store.CompanyCount, people.Count, store.PaymentCount);
    }

    private static CsvConfiguration Cfg() => new(CultureInfo.InvariantCulture)
    {
        Delimiter = ";",
        HasHeaderRecord = true,
        BadDataFound = null,
        MissingFieldFound = null,
        HeaderValidated = null,
        TrimOptions = TrimOptions.Trim,
    };

    private async Task<List<Company>> ReadCompaniesAsync(string path, CancellationToken ct)
    {
        var list = new List<Company>(capacity: 50_000);
        await using var fs = File.OpenRead(path);
        using var reader = new StreamReader(fs, Encoding.UTF8);
        using var csv = new CsvReader(reader, Cfg());

        await csv.ReadAsync();
        csv.ReadHeader();

        while (await csv.ReadAsync())
        {
            ct.ThrowIfCancellationRequested();

            var id = csv.GetField("SYNTHETIC_U") ?? "";
            if (string.IsNullOrWhiteSpace(id) || id.Count(c => c == '_') > 3)
                continue; // отсекаем битые тех. id из обезличивания

            list.Add(new Company
            {
                Id = id,
                OrgType = csv.GetField("ORG_TYPE"),
                OkfsName = csv.GetField("OKFS_NAME"),
                AgeBand = csv.GetField("COMPANY_AGE_YEARS_GRADATION"),
                EcoMark = csv.GetField("ECO_MARK_LABEL"),
                OkvedCode = csv.GetField("MAINOKVED_CODE"),
                OkvedName = csv.GetField("MAINOKVED_NAME"),
                HasKeyOwnerInfo = ParseBool(csv.GetField("NEGATIVEMARK")),
                WorkersFns = csv.GetField("AMOUNT_WORKERS_FNS_GRADATION"),
                WorkersSpark = csv.GetField("AMOUNT_WORKERS_SPARK_GRADATION"),
                ClientType = (csv.GetField("CL_TYPE") ?? "").Trim(),
                HasRko = ParseBool(csv.GetField("CL_PRODUCT_RKO_MARK")),
                HasGuarantee = ParseBool(csv.GetField("CL_PRODUCT_GAR_MARK")),
                HasLoan = ParseBool(csv.GetField("CL_PRODUCT_LOA_MARK")),
                HasLeasing = ParseBool(csv.GetField("CL_PRODUCT_LEA_MARK")),
                HasDeposit = ParseBool(csv.GetField("CL_PRODUCT_DEP_MARK")),
                HasPayroll = ParseBool(csv.GetField("CL_PRODUCT_ZPP_MARK")),
                FtsFlag = ParseBool(csv.GetField("FTS_FLAG")),
                FtsCountry = csv.GetField("FTS_COUNTRY_NAME"),
                HasFactoring = ParseBool(csv.GetField("FACTORING_MARK")),
                Revenue = ParseRevenue(csv.GetField("REVENUE_COMPANY")),
                GroupId = EmptyToNull(csv.GetField("GROUP_ID")),
                City = EmptyToNull(csv.GetField("CITY")),
                Region = EmptyToNull(csv.GetField("REGION")),
            });
        }

        return list;
    }

    private async Task<List<PersonLink>> ReadPeopleAsync(string path, CancellationToken ct)
    {
        var list = new List<PersonLink>();
        await using var fs = File.OpenRead(path);
        using var reader = new StreamReader(fs, Encoding.UTF8);
        using var csv = new CsvReader(reader, Cfg());

        await csv.ReadAsync();
        csv.ReadHeader();

        while (await csv.ReadAsync())
        {
            ct.ThrowIfCancellationRequested();
            var companyId = csv.GetField("SYNTHETIC_U") ?? "";
            var personId = csv.GetField("SYNTHETIC_F") ?? "";
            if (string.IsNullOrWhiteSpace(companyId) || string.IsNullOrWhiteSpace(personId))
                continue;

            list.Add(new PersonLink
            {
                PersonId = personId,
                City = EmptyToNull(csv.GetField("CITY_NAME")),
                Region = EmptyToNull(csv.GetField("RC_NAME")),
                CompanyId = companyId,
            });
        }

        return list;
    }

    private async Task<List<PaymentEdge>> ReadPaymentsAsync(string path, CancellationToken ct)
    {
        var list = new List<PaymentEdge>(capacity: 100_000);
        await using var fs = File.OpenRead(path);
        using var reader = new StreamReader(fs, Encoding.UTF8);
        using var csv = new CsvReader(reader, Cfg());

        await csv.ReadAsync();
        csv.ReadHeader();

        while (await csv.ReadAsync())
        {
            ct.ThrowIfCancellationRequested();
            var ben = csv.GetField("BENEFICIARY") ?? "";
            var payer = csv.GetField("PAYER") ?? "";
            if (string.IsNullOrWhiteSpace(ben) || string.IsNullOrWhiteSpace(payer))
                continue;

            list.Add(new PaymentEdge
            {
                BeneficiaryId = ben,
                PayerId = payer,
                Amount = ParseDecimal(csv.GetField("PAY_AMT")),
                Count = (int)ParseDecimal(csv.GetField("PAY_CNT")),
            });
        }

        return list;
    }

    private static bool ParseBool(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return false;

        raw = raw.Trim();
        if (raw is "1" or "1.0" or "Y" or "y" or "true" or "TRUE" or "True")
            return true;
        if (raw.Equals("да", StringComparison.OrdinalIgnoreCase))
            return true;

        // иногда прилетает 1.0 / 0.0
        if (decimal.TryParse(raw.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out var n))
            return n != 0;

        return false;
    }

    private static decimal? ParseRevenue(string? raw)
    {
        var v = ParseDecimal(raw);
        if (v <= 0)
            return null;
        if (v == 1000m)
            return null;
        return v;
    }

    private static decimal ParseDecimal(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return 0;

        raw = raw.Trim().Replace(" ", "").Replace(',', '.');
        return decimal.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var v)
            ? v
            : 0;
    }

    private static string? EmptyToNull(string? s) =>
        string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
