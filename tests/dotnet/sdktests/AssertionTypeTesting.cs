using System;
using System.Dynamic;
using System.Text;
using System.Text.Json;
using C2pa;

namespace sdktests
{
    public class AssertionTypeTesting
    {
        [Fact]
        public void TestingActionAssertTypeMaintainsDataDuringSerialization()
        {
            // Arrange 
            ActionAssertionData data = new()
            {
                Action = "Some Action",
                When = new DateTime(2024, 7, 18).ToString("yyyy-MM-ddTHH:mm:ss"),
                SoftwareAgent = "Some Software Agent",
                Changed = "Some Changed",
                InstanceID = "u11245151",
            };

            ActionAssertion assertion = new(data);

            // Act

            string json = JsonSerializer.Serialize(assertion, BaseAssertion.JsonOptions);

            ActionAssertion? result = JsonSerializer.Deserialize<ActionAssertion>(json, BaseAssertion.JsonOptions);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(assertion.Label, result.Label);
            Assert.Equal(data.Action, result.Data.Action);
            Assert.Equal(data.When, result.Data.When);
        }

        [Fact]
        public void TestCustomAssertionMaintainsDataDuringSerialization()
        {
            // Arrange
            var data = new {
                key1= "value1",
                key2= "value2",
                key3 = 1234,
                key4 = new
                {
                    key5 = true
                }
            };

            CustomAssertion assertion = new("Some Unique Label", data);
            // Act

            string json = JsonSerializer.Serialize(assertion, BaseAssertion.JsonOptions);

            CustomAssertion? result = JsonSerializer.Deserialize<CustomAssertion>(json, BaseAssertion.JsonOptions);
            dynamic? resultData = result?.GetDataAsExpandoObject();

            // Assert
            Assert.NotNull(result);
            Assert.NotNull(resultData);
            Assert.Equal(assertion.Label, result.Label);
            Assert.Equal(data.key1, resultData.key1);
            Assert.Equal(data.key4.key5, resultData.key4.key5);
        }
    }
}
