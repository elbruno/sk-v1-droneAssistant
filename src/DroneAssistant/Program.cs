using Microsoft.SemanticKernel.AI.ChatCompletion;
using Microsoft.SemanticKernel.Handlebars;

string OpenAIApiKey = Env.Var("OpenAI:ApiKey")!;
string BingApiKey = Env.Var("Bing:ApiKey")!;
string currentDirectory = Directory.GetCurrentDirectory();

// Initialize the required functions and services for the kernel
//IChatCompletion gpt = new OpenAIChatCompletion("gpt-4-1106-preview", OpenAIApiKey);
IChatCompletion gpt = new OpenAIChatCompletion("gpt-3.5-turbo-1106", OpenAIApiKey);


// ---------------------------------------------------
// RESEARCHER
// ---------------------------------------------------

IPlugin searchPlugin = new Plugin(
    name: "Search",
    functions: NativeFunction.GetFunctionsFromObject(new Search(BingApiKey))
);

// Create a researcher
IPlugin researcher = AssistantKernel.FromConfiguration(
    currentDirectory + "/Assistants/Researcher.agent.yaml",
    aiServices: new() { gpt, },
    plugins: new() { searchPlugin }
);

// ---------------------------------------------------
// DRONE CODE GENERATOR and C# PROGRAMMER Plugins
// ---------------------------------------------------
Plugin openAIChatCompletionDrone = new Plugin(
    name: "DroneCodeGenerator",
    functions: new() {
        SemanticFunction.GetFunctionFromYaml(currentDirectory + "/Plugins/TelloDrone/TelloDroneCS.prompt.yaml")
    }
);

IPlugin csharpCodeManagerPlugin = new Plugin(
    name: "CodeRun",
    functions: NativeFunction.GetFunctionsFromObject(new RunCode())
);

// ---------------------------------------------------
// DRONE PILOT Assistant
// ---------------------------------------------------

// Create a drone pilot assistant
IPlugin dronePilot = AssistantKernel.FromConfiguration(
    currentDirectory + "/Assistants/DronePilot.agent.yaml",
    aiServices: new() { gpt },
    plugins: new() { openAIChatCompletionDrone, csharpCodeManagerPlugin }
);

// Create a Project Manager
AssistantKernel projectManager = AssistantKernel.FromConfiguration(
    currentDirectory + "/Assistants/ProjectManager.agent.yaml",
    aiServices: new() { gpt },
    plugins: new() { researcher, dronePilot }
);


IThread thread = await projectManager.CreateThreadAsync();
bool keepRunning = true;
while (keepRunning)
{
    // Get user input
    Console.Write("User > ");
    string userInput = Console.ReadLine();

    // validate if the user wants to exit
    if (string.IsNullOrEmpty(userInput.ToLower()) || userInput.ToLower() == "exit")
    {
        keepRunning = false;
        continue;
    }

    // create a switch statement for userInput.ToLower()
    switch (userInput.ToLower())
    {
        case "d1":
            userInput = "send the drone the following actions: takeoff the drone, move forward 25 centimeters and land";
            break;
        case "d2":
            userInput = "send the drone the following actions: takeoff, move left 20 cms and land";
            break;
        default:
            break;
    }

    Console.WriteLine("Processing user input : " + userInput);
    _ = thread.AddUserMessageAsync(userInput);

    // Run the thread using the project manager kernel
    var result = await projectManager.RunAsync(thread);

    // Print the results
    var messages = result.GetValue<List<ModelMessage>>();
    foreach (ModelMessage message in messages)
    {
        Console.WriteLine("Project Manager > " + message);
    }
}
