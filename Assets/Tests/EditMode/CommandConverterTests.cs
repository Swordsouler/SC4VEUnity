using Newtonsoft.Json;
using NUnit.Framework;
using Sc4ve.Multimodality.Intent;
using System.Collections.Generic;
using System.Linq;

namespace Sc4ve.Tests.EditMode
{
    // Le CommandConverter (interne) est exercé via l'attribut [JsonConverter] de Command,
    // exactement comme le fait MultimodalityController.DeserializeCommand.
    public class CommandConverterTests
    {
        [Test]
        public void Deserialize_KnownCommandType_YieldsConcreteCommand()
        {
            const string json = @"[{""type"": ""DeleteCommand"", ""parameters"": []}]";
            List<Command> commands = JsonConvert.DeserializeObject<List<Command>>(json);
            Assert.AreEqual(1, commands.Count);
            Assert.IsInstanceOf<DeleteCommand>(commands[0]);
        }

        [Test]
        public void Deserialize_UnknownCommandType_YieldsUnknownCommand()
        {
            // Type halluciné par le LLM : doit produire UnknownCommand, jamais lever.
            const string json = @"[{""type"": ""TeleportCommand"", ""parameters"": []}]";
            List<Command> commands = JsonConvert.DeserializeObject<List<Command>>(json);
            Assert.AreEqual(1, commands.Count);
            Assert.IsInstanceOf<UnknownCommand>(commands[0]);
        }

        [Test]
        public void Deserialize_MissingTypeProperty_YieldsUnknownCommand()
        {
            const string json = @"[{""parameters"": []}]";
            List<Command> commands = JsonConvert.DeserializeObject<List<Command>>(json);
            Assert.AreEqual(1, commands.Count);
            Assert.IsInstanceOf<UnknownCommand>(commands[0]);
        }

        [Test]
        public void Deserialize_SelectionParameter_ParsesTypeAndStringLimit()
        {
            // « limit » est une chaîne dans le contrat LLM : elle doit être coercée en entier.
            const string json = @"[{""type"": ""SelectCommand"", ""parameters"": [
                {""type"": ""SelectionParameter"", ""filters"": [], ""limit"": ""3""}]}]";
            List<Command> commands = JsonConvert.DeserializeObject<List<Command>>(json);
            SelectionParameter selection = commands[0].Parameters.OfType<SelectionParameter>().FirstOrDefault();
            Assert.IsNotNull(selection, "Le paramètre doit être désérialisé en SelectionParameter.");
            Assert.AreEqual(3, selection.Limit);
        }

        [Test]
        public void Deserialize_MalformedJson_Throws()
        {
            const string json = @"[{""type"": ""DeleteCommand"", ";
            Assert.Catch<JsonException>(() => JsonConvert.DeserializeObject<List<Command>>(json));
        }
    }
}
