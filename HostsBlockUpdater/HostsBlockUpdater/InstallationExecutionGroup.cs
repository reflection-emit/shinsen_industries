using Cauldron.Consoles;
using Cauldron.Core;
using Microsoft.Win32.TaskScheduler;
using System;

namespace HostsBlockUpdater
{
    [ExecutionGroup("Installation")]
    public sealed class InstallationExecutionGroup : IExecutionGroup
    {
        private const string HostBlockTaskName = "Host Blocking Updater";

        [Parameter("Creates a scheduled task that executes the update at least once a day.", "task", "T")]
        public bool CreateATask { get; private set; }

        [Parameter("Removes the scheduled task if exist.", "remove-task", "R")]
        public bool RemoveTask { get; private set; }

        public void Execute(ParameterParser parser)
        {
            var parameters = parser.GetActiveParameters(this);

            if (parameters.Contains(nameof(CreateATask)))
            {
                if (!Win32Api.StartElevated(parser.Parameters))
                {
                    var task = TaskService.Instance.GetTask(HostBlockTaskName);

                    if (task != null)
                        TaskService.Instance.RootFolder.DeleteTask(HostBlockTaskName, false);

                    using (var taskService = new TaskService())
                    {
                        var taskDefinition = taskService.NewTask();
                        taskDefinition.Actions.Add(new ExecAction(typeof(InstallationExecutionGroup).Assembly.Location, "--u --H", ApplicationInfo.ApplicationPath.FullName));
                        taskDefinition.Triggers.Add(new LogonTrigger());
                        taskDefinition.Principal.RunLevel = TaskRunLevel.Highest;
                        taskDefinition.Principal.LogonType = TaskLogonType.ServiceAccount;
                        taskDefinition.Settings.DisallowStartIfOnBatteries = false;
                        taskDefinition.Principal.UserId = "SYSTEM";

                        taskService.RootFolder.RegisterTaskDefinition(HostBlockTaskName, taskDefinition);
                        TaskService.Instance.GetTask(HostBlockTaskName)?.Run();
                    }

                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.WriteLine("New Scheduled Task added: " + HostBlockTaskName);
                }
            }
            else if (parameters.Contains(nameof(RemoveTask)))
            {
                if (!Win32Api.StartElevated(parser.Parameters))
                {
                    TaskService.Instance.RootFolder.DeleteTask(HostBlockTaskName, false);

                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.WriteLine("Removed Scheduled Task: " + HostBlockTaskName);
                }
            }
        }
    }
}