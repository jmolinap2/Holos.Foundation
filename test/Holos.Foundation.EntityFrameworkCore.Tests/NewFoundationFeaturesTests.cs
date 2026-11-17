using NSubstitute;
using Abp.Localization;
using Holos.Foundation.Application;
using Holos.Foundation.Architecture;
using Holos.Foundation.Domain;

namespace Holos.Foundation.EntityFrameworkCore.Tests;

public class ForCompaniesTests
{
    private sealed record Row(long Id, long CompanyId) : IMustHaveCompany;

    private static readonly Row[] Rows = [new(1, 10), new(2, 20), new(3, 30)];

    [Fact]
    public void Keeps_only_rows_of_the_given_companies() =>
        Assert.Equal([1L, 3L], Rows.AsQueryable().ForCompanies([10, 30]).Select(r => r.Id).ToArray());

    [Fact]
    public void An_empty_list_returns_no_rows() =>
        Assert.Empty(Rows.AsQueryable().ForCompanies([]));

    [Fact]
    public void Repeated_ids_do_not_duplicate_rows() =>
        Assert.Single(Rows.AsQueryable().ForCompanies([10, 10, 10]));
}

public class LocalizableDomainExceptionTests
{
    [Fact]
    public void Exposes_key_and_arguments_and_logs_as_warning()
    {
        var error = new LocalizableDomainException("Clave", 1, "dos");
        Assert.Equal("Clave", error.LocalizationKey);
        Assert.Equal("Clave", error.Message);
        Assert.Equal(new object[] { 1, "dos" }, error.LocalizationArguments);
        Assert.Equal(global::Abp.Logging.LogSeverity.Warn, error.Severity);
    }

    [Fact]
    public void Requires_a_key() =>
        Assert.Throws<ArgumentException>(() => new LocalizableDomainException(" "));

    [Fact]
    public void Translator_formats_the_arguments_with_the_configured_source()
    {
        var translator = new LocalizableExceptionTranslator(FakeLocalization("Hola {0}, tienes {1:N0}"), new LocalizableExceptionOptions { SourceName = "Fuente" });
        Assert.Equal("Hola Ana, tienes 1,500", translator.Translate(new LocalizableDomainException("Saludo", "Ana", 1500)).Replace('.', ','));
    }

    [Fact]
    public void Translator_returns_the_text_as_is_without_arguments()
    {
        var translator = new LocalizableExceptionTranslator(FakeLocalization("Texto {0}"), new LocalizableExceptionOptions { SourceName = "Fuente" });
        Assert.Equal("Texto {0}", translator.Translate(new LocalizableDomainException("K")));
    }

    private static ILocalizationManager FakeLocalization(string text)
    {
        var source = NSubstitute.Substitute.For<global::Abp.Localization.Sources.ILocalizationSource>();
        source.GetString(NSubstitute.Arg.Any<string>()).Returns(text);
        var manager = NSubstitute.Substitute.For<ILocalizationManager>();
        manager.GetSource("Fuente").Returns(source);
        return manager;
    }
}

public class ArchitectureGuardTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "holos-guard-" + Guid.NewGuid().ToString("N"));

    public ArchitectureGuardTests() => Directory.CreateDirectory(_directory);
    public void Dispose() => Directory.Delete(_directory, true);

    private sealed class WithCompany : IMustHaveCompany { public long CompanyId { get; set; } }
    private sealed class WithoutDbSet : IMustHaveCompany { public long CompanyId { get; set; } }
    private sealed class DbSet<T> { }
    private sealed class FakeContext { public DbSet<WithCompany> Rows { get; } = new(); }

    [Fact]
    public void Finds_company_entities_without_a_DbSet_in_the_context()
    {
        var missing = CompanyFilterGuard.FindEntitiesWithoutDbSet(typeof(FakeContext), [typeof(ArchitectureGuardTests).Assembly]);
        Assert.Contains(typeof(WithoutDbSet).FullName, missing);
        Assert.DoesNotContain(typeof(WithCompany).FullName, missing);
    }

    [Fact]
    public void Finds_literal_messages_with_their_line_and_ignores_keys_and_obj()
    {
        File.WriteAllText(Path.Combine(_directory, "A.cs"), "class A {\n void M() {\n  throw new UserFriendlyException(\"texto\");\n  throw new LocalizableDomainException(\"Clave\");\n  throw new UserFriendlyException(L(\"Clave\"));\n }\n}");
        Directory.CreateDirectory(Path.Combine(_directory, "obj"));
        File.WriteAllText(Path.Combine(_directory, "obj", "B.cs"), "throw new UserFriendlyException(\"ignorado\");");

        Assert.Equal(["A.cs:3"], LiteralMessageGuard.FindLiteralMessages(_directory));
    }

    [Fact]
    public void Detects_missing_keys_duplicates_and_placeholder_changes()
    {
        File.WriteAllText(Path.Combine(_directory, "en.xml"), "<x><texts><text name=\"A\" value=\"Hi {0}\"/><text name=\"B\" value=\"Bye\"/><text name=\"B\" value=\"Bye2\"/></texts></x>");
        File.WriteAllText(Path.Combine(_directory, "es.xml"), "<x><texts><text name=\"A\" value=\"Hola\"/></texts></x>");
        var files = new[] { "en.xml", "es.xml" };
        var dictionaries = LocalizationDictionaryGuard.Load(_directory, files);

        Assert.Single(LocalizationDictionaryGuard.FindMissingKeys(dictionaries, "en.xml"));
        Assert.Single(LocalizationDictionaryGuard.FindDuplicatedKeys(_directory, files));
        Assert.Single(LocalizationDictionaryGuard.FindPlaceholderMismatches(dictionaries, "en.xml"));
    }
}
