# Github MCP Demo

**Github MCP Demo** is a sample implementation of a **Model Context Protocol (MCP) server** for GitHub that integrates with **OpenAI** to enable natural-language interactions with GitHub repositories.  
It demonstrates how to use MCP to bridge **GitHub data** (commits, pull requests, issues, repository content) with **chat-based AI completion**.

---

## 🚀 Features
- **MCP Server for GitHub** — fetch repository data via MCP requests.
- **Chat Completion with OpenAI** — query and interact with GitHub data conversationally.
- **Example Tools** — list repositories, retrieve commits, read file contents, etc.
- **Configurable** — easily point the MCP server to different GitHub repos and models.
- **Docker Support** — run locally or in a container.

---

## 📂 Project Structure
GithubMCPDemo/
├── GithubMCPDemo.csproj # Project file
├── Program.cs # Entry point
├── Services/ # MCP server logic
├── Models/ # Data models for GitHub responses
├── Dockerfile # Docker build instructions
├── README.md # This file

yaml
Kopyala
Düzenle

---

## 🛠️ Requirements
- [.NET 9 SDK](https://dotnet.microsoft.com/en-us/download)
- [Docker](https://www.docker.com/) (optional, for containerized run)
- GitHub **Personal Access Token**
- OpenAI API key

---

## ⚙️ Setup

### 1️⃣ Clone the Repository
```bash
git clone https://github.com/mustafaalkan64/GithubMCPDemo.git
cd GithubMCPDemo

2️⃣ Configure Environment Variables
Create a .env file or export these variables:
GITHUB_TOKEN=your_github_token
OPENAI_API_KEY=your_openai_api_key

3️⃣ Run Locally
dotnet restore
dotnet run

4️⃣ Run with Docker
docker build -t github-mcp-demo .
docker run -e GITHUB_TOKEN=your_github_token \
           -e OPENAI_API_KEY=your_openai_api_key \
           -p 5000:5000 github-mcp-demo
💬 Example Chat Queries
Once running, you can interact with the MCP server through your OpenAI client:

User: Show me the last 5 commits in mustafaalkan64/GithubMCPDemo.
AI: [Displays commit history with messages and authors]

User: Summarize the README file in the repository.
AI: [Generates a summary from the repo’s README.md]
🧩 How It Works
MCP Server handles requests to GitHub’s REST API.

OpenAI processes natural language queries and translates them into MCP commands.

Responses are fed back to the user with context-aware explanations or summaries.

📜 License
This project is licensed under the MIT License.

🤝 Contributing
Contributions, issues, and feature requests are welcome!
