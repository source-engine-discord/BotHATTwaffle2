using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BotHATTwaffle2.Models.LiteDB;
using BotHATTwaffle2.Util;

namespace BotHATTwaffle2.Services
{
    public class ToolsService
    {
        private List<Tool> _tools;
        public ToolsService()
        {
            LoadTools();
        }

        public void LoadTools()
        {
            _tools = DatabaseUtil.GetAllTools().ToList();
        }

        public List<Tool> GetTools()
        {
            return _tools;
        }

        public Tool GetTool(string command)
        {
            return _tools.FirstOrDefault(x => x.Command.Equals(command, StringComparison.OrdinalIgnoreCase));
        }
    }
}
