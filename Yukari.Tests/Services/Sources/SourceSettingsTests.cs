using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Yukari.Core.Models.Settings;
using Yukari.Services.Sources;

namespace Yukari.Tests.Services.Sources;

public class SourceSettingsTests
{
    private sealed record CustomUnknownSetting : SourceSetting;

    [Fact]
    public void ToggleSetting_WhenValid_ReturnsStoredValue()
    {
        // Arrange
        var schema = new List<SourceSetting>
        {
            new ToggleSetting { Key = "t1", Header = "Toggle 1", DefaultValue = false },
            new ToggleSetting { Key = "t2", Header = "Toggle 2", DefaultValue = true },
        };
        var stored = new Dictionary<string, string>
        {
            ["t1"] = "True",
            ["t2"] = "false",
        };

        // Act
        var settings = new SourceSettings(schema, stored);

        // Assert
        settings.GetBool("t1").Should().BeTrue();
        settings.GetBool("t2").Should().BeFalse();
    }

    [Fact]
    public void ToggleSetting_WhenInvalidOrMissing_ReturnsDeclaredDefault()
    {
        // Arrange
        var schema = new List<SourceSetting>
        {
            new ToggleSetting { Key = "t_invalid", Header = "Invalid", DefaultValue = true },
            new ToggleSetting { Key = "t_missing", Header = "Missing", DefaultValue = false },
        };
        var stored = new Dictionary<string, string>
        {
            ["t_invalid"] = "not_a_bool",
        };

        // Act
        var settings = new SourceSettings(schema, stored);

        // Assert
        settings.GetBool("t_invalid").Should().BeTrue();
        settings.GetBool("t_missing").Should().BeFalse();
    }

    [Fact]
    public void TextSetting_WhenPresent_ReturnsStoredValue_WhenMissing_ReturnsDeclaredDefault()
    {
        // Arrange
        var schema = new List<SourceSetting>
        {
            new TextSetting { Key = "txt1", Header = "Text 1", DefaultValue = "default" },
            new TextSetting { Key = "txt2", Header = "Text 2", DefaultValue = "def2" },
        };
        var stored = new Dictionary<string, string>
        {
            ["txt1"] = "custom_value",
        };

        // Act
        var settings = new SourceSettings(schema, stored);

        // Assert
        settings.GetString("txt1").Should().Be("custom_value");
        settings.GetString("txt2").Should().Be("def2");
    }

    [Fact]
    public void PasswordSetting_WhenPresent_ReturnsValue_WhenMissing_ReturnsEmpty()
    {
        // Arrange
        var schema = new List<SourceSetting>
        {
            new PasswordSetting { Key = "pwd1", Header = "Password 1" },
            new PasswordSetting { Key = "pwd2", Header = "Password 2" },
        };
        var stored = new Dictionary<string, string>
        {
            ["pwd1"] = "secret123",
        };

        // Act
        var settings = new SourceSettings(schema, stored);

        // Assert
        settings.GetString("pwd1").Should().Be("secret123");
        settings.GetString("pwd2").Should().Be(string.Empty);
    }

    [Fact]
    public void NumberSetting_ParsesInvariantCulture_AndClampsToMinMax()
    {
        // Arrange
        var schema = new List<SourceSetting>
        {
            new NumberSetting
            {
                Key = "n_normal",
                Header = "Normal",
                DefaultValue = 10,
                Minimum = 1,
                Maximum = 100,
            },
            new NumberSetting
            {
                Key = "n_low",
                Header = "Too Low",
                DefaultValue = 10,
                Minimum = 5,
                Maximum = 50,
            },
            new NumberSetting
            {
                Key = "n_high",
                Header = "Too High",
                DefaultValue = 10,
                Minimum = 5,
                Maximum = 50,
            },
            new NumberSetting
            {
                Key = "n_invalid",
                Header = "Invalid",
                DefaultValue = 42,
                Minimum = 0,
                Maximum = 100,
            },
        };
        var stored = new Dictionary<string, string>
        {
            ["n_normal"] = "15.75",
            ["n_low"] = "-10",
            ["n_high"] = "999",
            ["n_invalid"] = "not_a_number",
        };

        // Act
        var settings = new SourceSettings(schema, stored);

        // Assert
        settings.GetDouble("n_normal").Should().Be(15.75);
        settings.GetInt("n_normal").Should().Be(16); // Rounded
        settings.GetDouble("n_low").Should().Be(5);
        settings.GetDouble("n_high").Should().Be(50);
        settings.GetDouble("n_invalid").Should().Be(42);
    }

    [Fact]
    public void SingleChoiceSetting_WhenValid_ReturnsStoredOption_WhenInvalidOrMissing_ReturnsDefault()
    {
        // Arrange
        var options = new List<SettingOption>
        {
            new("opt1", "Option 1"),
            new("opt2", "Option 2"),
        };
        var schema = new List<SourceSetting>
        {
            new SingleChoiceSetting
            {
                Key = "sc_valid",
                Header = "Valid",
                Options = options,
                DefaultKey = "opt1",
            },
            new SingleChoiceSetting
            {
                Key = "sc_invalid",
                Header = "Invalid",
                Options = options,
                DefaultKey = "opt1",
            },
            new SingleChoiceSetting
            {
                Key = "sc_missing",
                Header = "Missing",
                Options = options,
                DefaultKey = "opt2",
            },
        };
        var stored = new Dictionary<string, string>
        {
            ["sc_valid"] = "opt2",
            ["sc_invalid"] = "opt_unknown",
        };

        // Act
        var settings = new SourceSettings(schema, stored);

        // Assert
        settings.GetString("sc_valid").Should().Be("opt2");
        settings.GetString("sc_invalid").Should().Be("opt1");
        settings.GetString("sc_missing").Should().Be("opt2");
    }

    [Fact]
    public void MultipleChoiceSetting_FiltersUnknownOptions_AndFallsBackOnInvalidJson()
    {
        // Arrange
        var options = new List<SettingOption>
        {
            new("a", "Option A"),
            new("b", "Option B"),
            new("c", "Option C"),
        };
        var schema = new List<SourceSetting>
        {
            new MultipleChoiceSetting
            {
                Key = "mc_valid",
                Header = "Valid",
                Options = options,
                DefaultKeys = ["a"],
            },
            new MultipleChoiceSetting
            {
                Key = "mc_filtered",
                Header = "Filtered",
                Options = options,
                DefaultKeys = ["a"],
            },
            new MultipleChoiceSetting
            {
                Key = "mc_invalid_json",
                Header = "Invalid JSON",
                Options = options,
                DefaultKeys = ["b", "c"],
            },
            new MultipleChoiceSetting
            {
                Key = "mc_missing",
                Header = "Missing",
                Options = options,
                DefaultKeys = ["a", "c"],
            },
        };
        var stored = new Dictionary<string, string>
        {
            ["mc_valid"] = "[\"a\",\"b\"]",
            ["mc_filtered"] = "[\"a\",\"unknown_key\",\"c\"]",
            ["mc_invalid_json"] = "not json",
        };

        // Act
        var settings = new SourceSettings(schema, stored);

        // Assert
        settings.GetStringList("mc_valid").Should().BeEquivalentTo(["a", "b"]);
        settings.GetStringList("mc_filtered").Should().BeEquivalentTo(["a", "c"]);
        settings.GetStringList("mc_invalid_json").Should().BeEquivalentTo(["b", "c"]);
        settings.GetStringList("mc_missing").Should().BeEquivalentTo(["a", "c"]);
    }

    [Fact]
    public void UndeclaredKeys_AndMismatchedTypes_ReturnCallerFallbackDefaults()
    {
        // Arrange
        var schema = new List<SourceSetting>
        {
            new ToggleSetting { Key = "my_toggle", Header = "Toggle", DefaultValue = true },
            new TextSetting { Key = "my_text", Header = "Text", DefaultValue = "hello" },
        };
        var stored = new Dictionary<string, string>
        {
            ["my_toggle"] = "true",
            ["my_text"] = "hello",
            ["stale_key"] = "stale_value",
        };

        // Act
        var settings = new SourceSettings(schema, stored);

        // Assert
        // Stale / undeclared keys return caller defaults
        settings.GetBool("stale_key", defaultValue: false).Should().BeFalse();
        settings.GetString("stale_key", defaultValue: "fallback").Should().Be("fallback");
        settings.GetInt("stale_key", defaultValue: 99).Should().Be(99);
        settings.GetDouble("stale_key", defaultValue: 1.23).Should().Be(1.23);
        settings.GetStringList("stale_key", ["fallback_list"]).Should().BeEquivalentTo(["fallback_list"]);

        // Mismatched types return caller defaults
        settings.GetString("my_toggle", "mismatched").Should().Be("mismatched");
        settings.GetBool("my_text", false).Should().BeFalse();
    }

    [Fact]
    public void UnknownSourceSettingSubclass_IsIgnoredWithoutThrowing()
    {
        // Arrange
        var schema = new List<SourceSetting>
        {
            new CustomUnknownSetting { Key = "custom", Header = "Custom" },
            new ToggleSetting { Key = "valid_toggle", Header = "Valid", DefaultValue = true },
        };
        var stored = new Dictionary<string, string>
        {
            ["custom"] = "some_value",
            ["valid_toggle"] = "false",
        };

        // Act
        var act = () => new SourceSettings(schema, stored, NullLogger.Instance);

        // Assert
        act.Should().NotThrow();
        var settings = act();
        settings.GetBool("valid_toggle").Should().BeFalse();
        settings.GetString("custom", "fallback").Should().Be("fallback");
    }
}
