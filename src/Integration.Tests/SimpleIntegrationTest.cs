using System.IO;
using NUnit.Framework;
using RunProcess;

namespace Integration.Tests
{
    [TestFixture]
    public class SimpleIntegrationTest
    {

        [Test]
        public void can_start_interact_with_and_stop_a_process ()
        {
            var subject = new InteractiveShell(">", "bye");
            subject.Start("./ExampleProcess.exe", Directory.GetCurrentDirectory());

            var intro = subject.ReadToPrompt();
            Assert.That(intro.Item1, Is.StringStarting(ExampleProcess.Program.Intro));

            var interact = subject.SendAndReceive("This is a test");
            Assert.That(interact.Item1, Is.StringStarting("You wrote This is a test"));

            subject.Terminate();
        }
    }
}
