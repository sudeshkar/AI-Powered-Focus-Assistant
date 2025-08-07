# app.py – Flask API with integrated RL agent
from flask import Flask, request, jsonify
from flask_cors import CORS
from datetime import datetime, timedelta
from collections import defaultdict, deque
import os

# -------------  NEW: bring in the RL service -------------
from rl_agent import RLIntegrationService
# --------------------------------------------------------

app = Flask(__name__)
CORS(app)

# ------------------------------------------------------------------
# Core API class (now using the RL agent for every decision)
# ------------------------------------------------------------------
class FocusAssistantAPI:
    def __init__(self):
        self.user_data   = defaultdict(list)          # date -> [activities]
        self.recent_acts = deque(maxlen=100)          # last 100 raw activities
        self.interventions = []                       # feedback history

        # -------- RL integration --------
        self.rl = RLIntegrationService()
        print("🧠 RL Agent initialized and ready for personalized learning!")

    # ----------------- helpers -----------------
    def _now(self): return datetime.now()

    def _date_key(self): return self._now().strftime('%Y-%m-%d')

    def _log_activity(self, act):
        act['timestamp'] = self._now().isoformat()
        self.recent_acts.append(act)
        self.user_data[self._date_key()].append(act)

    def _recent_productivity_score(self):
        recent = list(self.recent_acts)[-10:]
        if not recent: return 0.5
        productive = sum(1 for a in recent if a.get('is_productive', False))
        return productive / len(recent)

    def _recent_app_switches(self, minutes):
        cutoff = self._now() - timedelta(minutes=minutes)
        apps   = [a['app_name'] for a in self.recent_acts
                  if datetime.fromisoformat(a['timestamp']) >= cutoff]
        return sum(a != b for a, b in zip(apps, apps[1:]))

    def _session_duration_mins(self):
        if not self.recent_acts: return 0
        cur = self.recent_acts[-1]['app_name']
        dur = 0
        for a in reversed(self.recent_acts):
            if a['app_name'] != cur: break
            dur += 1               # 1 activity ≈ 1 minute (same as original)
        return dur
    # ------------------------------------------

    # -------------- RL-powered analysis --------------
    def analyze(self, app_name, window_title, is_productive):
        context = {
            'recent_productivity_score': self._recent_productivity_score(),
            'app_switches_last_hour':   self._recent_app_switches(60),
            'current_session_duration': self._session_duration_mins()
        }
        activity = {
            'app_name':        app_name,
            'window_title':    window_title,
            'is_productive':   is_productive,
            'duration_minutes': context['current_session_duration']
        }
        return self.rl.process_activity(activity, context)
    # -------------------------------------------------

    # -------------- RL-powered suggestions --------------
    def suggestions(self):
        ctx = {
            'recent_productivity_score': self._recent_productivity_score(),
            'app_switches_last_hour':   self._recent_app_switches(60),
            'current_hour':             self._now().hour
        }
        return self.rl.get_suggestions(ctx)
    # ---------------------------------------------------

# -------------------- singleton --------------------
api = FocusAssistantAPI()
# ---------------------------------------------------

# -------------------- routes -----------------------
@app.route('/health', methods=['GET'])
def health():
    return jsonify({'status': 'healthy', 'timestamp': datetime.now().isoformat()})

@app.route('/activity', methods=['POST'])
def log_activity():
    try:
        data = request.get_json()
        for k in ('app_name', 'window_title', 'is_productive'):
            if k not in data:
                return jsonify({'error': f'Missing field: {k}'}), 400

        api._log_activity(data)
        rl_result = api.analyze(data['app_name'], data['window_title'], data['is_productive'])

        return jsonify({
            'status': 'success',
            'distraction_risk': rl_result['distraction_risk'],
            'intervention_message': rl_result['intervention_message'],
            'action_taken': rl_result['action_taken'],
            'intervention_id': rl_result['intervention_id'],
            'timestamp': datetime.now().isoformat()
        })
    except Exception as e:
        return jsonify({'error': str(e)}), 500

@app.route('/suggestions', methods=['GET'])
def get_suggestions():
    try:
        return jsonify({
            'status': 'success',
            'suggestions': api.suggestions(),
            'timestamp': datetime.now().isoformat()
        })
    except Exception as e:
        return jsonify({'error': str(e)}), 500

@app.route('/analytics', methods=['GET'])
def analytics():
    try:
        today = api._date_key()
        acts = api.user_data.get(today, [])
        prod = sum(1 for a in acts if a.get('is_productive', False))
        rate = (prod / len(acts)) * 100 if acts else 0

        app_cnt = defaultdict(int)
        for a in acts:
            app_cnt[a.get('app_name', 'unknown')] += 1

        return jsonify({
            'status': 'success',
            'date': today,
            'total_activities': len(acts),
            'productivity_rate': round(rate, 1),
            'top_apps': dict(sorted(app_cnt.items(), key=lambda x: x[1], reverse=True)[:5]),
            'recent_interventions': len(api.interventions),
            'timestamp': datetime.now().isoformat()
        })
    except Exception as e:
        return jsonify({'error': str(e)}), 500

@app.route('/feedback', methods=['POST'])
def feedback():
    try:
        data = request.get_json()
        intervention_id = data.get('intervention_id')
        if intervention_id:
            feedback_payload = {
                'helpful': data.get('helpful', False),
                'user_action': data.get('action', 'none'),
                'productivity_change': data.get('productivity_change', 0)
            }
            api.rl.process_feedback(intervention_id, feedback_payload)
            print(f"🧠 RL feedback processed: {feedback_payload['user_action']}")

        api.interventions.append({
            'timestamp': datetime.now().isoformat(),
            **data
        })
        return jsonify({'status': 'success', 'message': 'Feedback recorded'})
    except Exception as e:
        return jsonify({'error': str(e)}), 500

@app.route('/insights', methods=['GET'])
def insights():
    try:
        return jsonify({
            'status': 'success',
            'insights': api.rl.get_model_insights(),
            'timestamp': datetime.now().isoformat()
        })
    except Exception as e:
        return jsonify({'error': str(e)}), 500
# ---------------------------------------------------

if __name__ == '__main__':
    print("🚀 Starting AI Focus Assistant API with RL Agent…")
    app.run(host='127.0.0.1', port=5000, debug=True, threaded=True)