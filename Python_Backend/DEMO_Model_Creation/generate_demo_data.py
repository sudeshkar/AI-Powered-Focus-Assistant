import random
import json

apps = [
    {"app_name": "Visual Studio Code", "window_titles": ["main.py - ProjectX", "app.js - WebApp", "script.py - AI Model"], "is_productive": True},
    {"app_name": "Microsoft Word", "window_titles": ["Research Paper.docx", "Assignment.docx"], "is_productive": True},
    {"app_name": "Chrome", "window_titles": ["StackOverflow - Python Q&A", "Research - AI papers"], "is_productive": True},
    {"app_name": "YouTube", "window_titles": ["Funny cat videos", "Gaming highlights"], "is_productive": False},
    {"app_name": "Slack", "window_titles": ["Team Chat", "Project Updates"], "is_productive": False}
]

def generate_session():
    app = random.choice(apps)
    return {
        "app_name": app["app_name"],
        "window_title": random.choice(app["window_titles"]),
        "is_productive": app["is_productive"]
    }

# Generate 500 sessions
dataset = [generate_session() for _ in range(500)]

with open("demo_sessions.json", "w") as f:
    json.dump(dataset, f, indent=4)

print("✅ 500 demo sessions generated in minimal format")
