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
Console.WriteLine("Welcome to El Bruno Drone Assistant.");
Console.WriteLine("Please type your questions, and if you want to fly the DJI Tello drone, type the commands.");
Console.WriteLine("Type exit to finish the program.");
Console.WriteLine(" ");
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

    // sample user input for demo purposes
    switch (userInput.ToLower())
    {
        case "d1":
            userInput = "send the drone the following actions: takeoff the drone, move forward 25 centimeters and land";
            break;
        case "d2":
            userInput = "send the drone the following actions: takeoff, bounce and land";
            break;
        case "d3":
            userInput = "send the drone the following actions: takeoff, flip forward and land";
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
