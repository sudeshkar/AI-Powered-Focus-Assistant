import requests
import json
import random
import time

# ---------------- Configuration ----------------
API_ACTIVITY_URL = "http://127.0.0.1:5000/activity"
API_FEEDBACK_URL = "http://127.0.0.1:5000/feedback"
DELAY = 0.2              # delay between batches
BATCH_SIZE = 5
ACTIONS = ["ActedImmediately", "ActedLater", "DismissedPolitely", "DismissedAnnoyed", "Ignored"]
# ------------------------------------------------

# Load demo sessions
with open("demo_sessions.json") as f:
    dataset = json.load(f)

for i in range(0, len(dataset), BATCH_SIZE):
    batch = dataset[i:i+BATCH_SIZE]
    for session in batch:
        # 1️⃣ Send activity
        try:
            response = requests.post(API_ACTIVITY_URL, json=session)
            if response.status_code != 200:
                print(f"❌ Activity failed: {response.status_code} | {response.text}")
                continue
            result = response.json()
            intervention_id = result.get("intervention_id")
            print(f"➡ Activity sent: {session['app_name']} | Suggestion: {result.get('intervention_message')}")

            # 2️⃣ Send feedback if intervention_id exists
            if intervention_id:
                feedback_payload = {
                    "helpful": random.choice([True, False]),
                    "action": random.choice(ACTIONS),
                    "intervention_id": intervention_id,
                    "productivity_change": random.randint(0, 10)
                }
                fb_response = requests.post(API_FEEDBACK_URL, json=feedback_payload)
                if fb_response.status_code == 200:
                    print(f"✅ Feedback sent: {feedback_payload['action']}")
                else:
                    print(f"❌ Feedback failed: {fb_response.status_code} | {fb_response.text}")

        except Exception as e:
            print(f"❌ Exception: {e}")

    time.sleep(DELAY)

print("🚀 All activities + feedback sent successfully!")
