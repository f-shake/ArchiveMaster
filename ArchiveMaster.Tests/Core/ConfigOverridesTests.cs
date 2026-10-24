using System;
using System.Collections.Generic;
using ArchiveMaster.Cli;
using ArchiveMaster.Configs;
using ArchiveMaster.Enums;

namespace ArchiveMaster.Tests.Core;

public class ConfigOverridesTests
{
    [Fact]
    public void Apply_TopLevelString_SetsValue()
    {
        var config = new TimeClassifyConfig();

        ConfigOverrides.Apply(config, "fileTools.timeClassify", [@"Dir=D:\input"]);

        Assert.Equal(@"D:\input", config.Dir);
    }

    [Fact]
    public void Apply_DuplicateField_LastValueWins()
    {
        var config = new TimeClassifyConfig();

        ConfigOverrides.Apply(config, "fileTools.timeClassify", ["Dir=A", "Dir=B"]);

        Assert.Equal("B", config.Dir);
    }

    [Fact]
    public void Apply_FieldNameIsCaseInsensitive()
    {
        var config = new TimeClassifyConfig();

        ConfigOverrides.Apply(config, "fileTools.timeClassify", [@"dir=D:\input"]);

        Assert.Equal(@"D:\input", config.Dir);
    }

    [Fact]
    public void Apply_ValueContainsEquals_SplitsOnFirstEquals()
    {
        var config = new TimeClassifyConfig();

        ConfigOverrides.Apply(config, "fileTools.timeClassify", [@"Dir=D:\a=b"]);

        Assert.Equal(@"D:\a=b", config.Dir);
    }

    [Fact]
    public void Apply_EmptyString_SetsEmptyString()
    {
        var config = new TimeClassifyConfig();

        ConfigOverrides.Apply(config, "fileTools.timeClassify", ["Dir="]);

        Assert.Equal(string.Empty, config.Dir);
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("1", true)]
    [InlineData("false", false)]
    [InlineData("0", false)]
    public void Apply_Boolean_ParsesSupportedForms(string raw, bool expected)
    {
        var config = new DirStructureSyncConfig();

        ConfigOverrides.Apply(config, "fileTools.dirStructureSync", [$"CompareName={raw}"]);

        Assert.Equal(expected, config.CompareName);
    }

    [Fact]
    public void Apply_Enum_ParsesName()
    {
        var config = new FileFilterOperationConfig();

        ConfigOverrides.Apply(config, "fileTools.fileFilterOperation", ["Type=Move"]);

        Assert.Equal(FileFilterOperationType.Move, config.Type);
    }

    [Fact]
    public void Apply_Enum_ParsesNumber()
    {
        var config = new FileFilterOperationConfig();

        ConfigOverrides.Apply(config, "fileTools.fileFilterOperation",
            [$"Type={(int)FileFilterOperationType.Move}"]);

        Assert.Equal(FileFilterOperationType.Move, config.Type);
    }

    [Fact]
    public void Apply_TimeSpan_Parses()
    {
        var config = new TimeClassifyConfig();

        ConfigOverrides.Apply(config, "fileTools.timeClassify", ["MinTimeInterval=00:30:00"]);

        Assert.Equal(TimeSpan.FromMinutes(30), config.MinTimeInterval);
    }

    [Fact]
    public void Apply_NullableDateTime_Parses()
    {
        var config = new FileFilterOperationConfig();

        ConfigOverrides.Apply(config, "fileTools.fileFilterOperation",
            ["EarliestTime=2026-01-02T03:04:05"]);

        Assert.Equal(new DateTime(2026, 1, 2, 3, 4, 5), config.EarliestTime);
    }

    [Fact]
    public void Apply_ScalarTypes_Parses()
    {
        var config = new ScalarConfig();

        ConfigOverrides.Apply(config, "test.scalar",
        [
            "Id=6f9619ff-8b86-d011-b42d-00c04fc964ff",
            "Timestamp=2026-01-02T03:04:05+08:00",
            "Amount=12.34",
            "Count=7",
            "Ratio=1.5",
            "DoubleValue=2.25",
            "Letter=Z",
            "NullableFlag=true",
            "NullableLong=1234567890123",
            "NullableInt=42",
        ]);

        Assert.Equal(Guid.Parse("6f9619ff-8b86-d011-b42d-00c04fc964ff"), config.Id);
        Assert.Equal(new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.FromHours(8)), config.Timestamp);
        Assert.Equal(12.34m, config.Amount);
        Assert.Equal(7, config.Count);
        Assert.Equal(1.5f, config.Ratio);
        Assert.Equal(2.25d, config.DoubleValue);
        Assert.Equal('Z', config.Letter);
        Assert.True(config.NullableFlag);
        Assert.Equal(1234567890123L, config.NullableLong);
        Assert.Equal(42, config.NullableInt);
    }

    [Fact]
    public void Apply_EmptyNonString_IsRejected()
    {
        var ex = Assert.Throws<CliException>(() => ConfigOverrides.Apply(
            new TimeClassifyConfig(), "fileTools.timeClassify", ["MinTimeInterval="]));

        Assert.Equal(2, ex.ExitCode);
        Assert.Contains("TimeSpan", ex.Message);
    }

    [Fact]
    public void Apply_IntermediateScalar_IsRejected()
    {
        var ex = Assert.Throws<CliException>(() => ConfigOverrides.Apply(
            new TimeClassifyConfig { Dir = "abc" }, "fileTools.timeClassify", ["Dir.Length=5"]));

        Assert.Equal(2, ex.ExitCode);
        Assert.Contains("不是对象", ex.Message);
    }

    [Fact]
    public void Apply_UncreatableNestedObject_IsRejected()
    {
        var ex = Assert.Throws<CliException>(() => ConfigOverrides.Apply(
            new UncreatableNestedConfig(), "test.uncreatable", ["Nested.Value=abc"]));

        Assert.Equal(2, ex.ExitCode);
        Assert.Contains("上级对象为空", ex.Message);
    }

    [Fact]
    public void Apply_NestedField_SetsValue()
    {
        var config = new FileFilterOperationConfig();

        ConfigOverrides.Apply(config, "fileTools.fileFilterOperation",
            ["PathFilter.IncludeFiles=*.jpg"]);

        Assert.Equal("*.jpg", config.PathFilter.IncludeFiles);
    }

    [Fact]
    public void Apply_ComplexObject_DeserializesJson()
    {
        var config = new FileFilterOperationConfig();

        ConfigOverrides.Apply(config, "fileTools.fileFilterOperation",
            ["PathFilter={\"IncludeFiles\":\"*.png\",\"UseRegex\":true}"]);

        Assert.Equal("*.png", config.PathFilter.IncludeFiles);
        Assert.True(config.PathFilter.UseRegex);
    }

    [Fact]
    public void Apply_Collection_DeserializesJson()
    {
        var config = new CollectionConfig();

        ConfigOverrides.Apply(config, "test.collection", ["Items=[\"a\",\"b\"]"]);

        Assert.Equal(["a", "b"], config.Items);
    }

    [Fact]
    public void Apply_NullNestedObject_CreatesInstance()
    {
        var config = new NestedConfig();

        ConfigOverrides.Apply(config, "test.nested", ["Nested.Value=abc"]);

        Assert.NotNull(config.Nested);
        Assert.Equal("abc", config.Nested.Value);
    }

    [Fact]
    public void Apply_StringNull_IsLiteral()
    {
        var config = new TimeClassifyConfig();

        ConfigOverrides.Apply(config, "fileTools.timeClassify", ["Dir=null"]);

        Assert.Equal("null", config.Dir);
    }

    [Fact]
    public void Apply_ComplexNull_IsRejected()
    {
        var ex = Assert.Throws<CliException>(() => ConfigOverrides.Apply(
            new FileFilterOperationConfig(), "fileTools.fileFilterOperation", ["PathFilter=null"]));

        Assert.Equal(2, ex.ExitCode);
        Assert.Contains("null", ex.Message);
    }

    [Fact]
    public void Apply_SecurePassword_AcceptsPlaintext()
    {
        var config = new EncryptorConfig();

        ConfigOverrides.Apply(config, "fileTools.encryptor", ["Password=secret"]);

        Assert.Equal("secret", config.Password.Password);
    }

    [Fact]
    public void Apply_SecurePassword_AcceptsJsonString()
    {
        var config = new EncryptorConfig();

        ConfigOverrides.Apply(config, "fileTools.encryptor", ["Password=\"secret\""]);

        Assert.Equal("secret", config.Password.Password);
    }

    [Fact]
    public void Apply_SecurePassword_HexPlaintextIsPreserved()
    {
        string hex = new('a', 64);
        var config = new EncryptorConfig();

        ConfigOverrides.Apply(config, "fileTools.encryptor", [$"Password={hex}"]);

        Assert.Equal(hex, config.Password.Password);
    }

    [Fact]
    public void Apply_SecurePassword_JsonObjectIsRejected()
    {
        var ex = Assert.Throws<CliException>(() => ConfigOverrides.Apply(
            new EncryptorConfig(), "fileTools.encryptor", ["Password={\"Password\":\"x\"}"]));

        Assert.Equal(2, ex.ExitCode);
        Assert.Contains("只接受明文或 JSON 字符串", ex.Message);
    }

    [Fact]
    public void Apply_SecurePasswordNull_IsRejected()
    {
        var ex = Assert.Throws<CliException>(() => ConfigOverrides.Apply(
            new EncryptorConfig(), "fileTools.encryptor", ["Password=null"]));

        Assert.Equal(2, ex.ExitCode);
        Assert.Contains("null", ex.Message);
    }

    [Fact]
    public void Apply_InvalidJson_IsRejected()
    {
        var ex = Assert.Throws<CliException>(() => ConfigOverrides.Apply(
            new FileFilterOperationConfig(), "fileTools.fileFilterOperation",
            ["PathFilter={bad}"]));

        Assert.Equal(2, ex.ExitCode);
        Assert.Contains("JSON", ex.Message);
    }

    [Fact]
    public void Apply_MissingEquals_IsRejected()
    {
        var ex = Assert.Throws<CliException>(() => ConfigOverrides.Apply(
            new TimeClassifyConfig(), "fileTools.timeClassify", ["Dir"]));

        Assert.Equal(2, ex.ExitCode);
        Assert.Contains("字段=值", ex.Message);
    }

    [Fact]
    public void Apply_EmptyFieldName_IsRejected()
    {
        var ex = Assert.Throws<CliException>(() => ConfigOverrides.Apply(
            new TimeClassifyConfig(), "fileTools.timeClassify", ["=x"]));

        Assert.Equal(2, ex.ExitCode);
        Assert.Contains("字段名不能为空", ex.Message);
    }

    [Fact]
    public void Apply_UnknownField_IsRejected()
    {
        var ex = Assert.Throws<CliException>(() => ConfigOverrides.Apply(
            new TimeClassifyConfig(), "fileTools.timeClassify", ["NoSuchField=x"]));

        Assert.Equal(2, ex.ExitCode);
        Assert.Contains("没有配置字段", ex.Message);
    }

    [Fact]
    public void Apply_ReadOnlyField_IsRejected()
    {
        var ex = Assert.Throws<CliException>(() => ConfigOverrides.Apply(
            new ReadOnlyConfig(), "test.readOnly", ["ReadOnly=x"]));

        Assert.Equal(2, ex.ExitCode);
        Assert.Contains("只读", ex.Message);
    }

    [Fact]
    public void Apply_InvalidScalar_IsRejected()
    {
        var ex = Assert.Throws<CliException>(() => ConfigOverrides.Apply(
            new TimeClassifyConfig(), "fileTools.timeClassify", ["MinTimeInterval=abc"]));

        Assert.Equal(2, ex.ExitCode);
        Assert.Contains("TimeSpan", ex.Message);
    }

    [Fact]
    public void Apply_NoAssignments_DoesNotChangeConfig()
    {
        var config = new TimeClassifyConfig { Dir = "original" };

        ConfigOverrides.Apply(config, "fileTools.timeClassify", Array.Empty<string>());

        Assert.Equal("original", config.Dir);
    }

    private sealed class ReadOnlyConfig : ConfigBase
    {
        public string ReadOnly => "x";

        public override void Check()
        {
        }
    }

    private sealed class CollectionConfig : ConfigBase
    {
        public List<string> Items { get; set; } = [];

        public override void Check()
        {
        }
    }

    private sealed class NestedConfig : ConfigBase
    {
        public NestedValue Nested { get; set; }

        public override void Check()
        {
        }
    }

    private sealed class NestedValue
    {
        public string Value { get; set; }
    }

    private sealed class UncreatableNestedConfig : ConfigBase
    {
        public UncreatableNested Nested { get; set; }

        public override void Check()
        {
        }
    }

    private sealed class UncreatableNested
    {
        public UncreatableNested(string value)
        {
        }

        public string Value { get; set; }
    }

    private sealed class ScalarConfig : ConfigBase
    {
        public Guid Id { get; set; }

        public DateTimeOffset Timestamp { get; set; }

        public decimal Amount { get; set; }

        public int Count { get; set; }

        public float Ratio { get; set; }

        public double DoubleValue { get; set; }

        public char Letter { get; set; }

        public bool? NullableFlag { get; set; }

        public long? NullableLong { get; set; }

        public int? NullableInt { get; set; }

        public override void Check()
        {
        }
    }
}
