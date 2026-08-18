# app.py - Flask API fronting the reinforcement-learning focus agent.
import traceback
from collections import defaultdict, deque
from datetime import datetime, timedelta

from flask import Flask, jsonify, request
from flask_cors import CORS

import config
from rl_agent import RLIntegrationService

app = Flask(__name__)

# The only consumer is the WPF client on the loopback interface.
CORS(app, origins=[f"http://{config.HOST}:{config.PORT}", "http://127.0.0.1", "http://localhost"])

# Keep unbounded history from growing for the process lifetime.
MAX_INTERVENTION_HISTORY = 500
MAX_TRACKED_DAYS = 14


class FocusAssistantAPI:
    """In-memory activity log plus the RL agent that decides on interventions.

    Activity history is deliberately not persisted: the WPF client owns durable
    history in SQLite. Only the agent's learned state survives a restart.
    """

    def __init__(self):
        self.user_data = defaultdict(list)                             # date -> [activities]
        self.recent_acts = deque(maxlen=100)                           # last 100 raw activities
        self.interventions = deque(maxlen=MAX_INTERVENTION_HISTORY)    # feedback history
        self.rl = RLIntegrationService()
        print("RL agent initialised and ready.")

    # ----------------- helpers -----------------
    def _now(self):
        return datetime.now()

    def _date_key(self):
        return self._now().strftime('%Y-%m-%d')

    def _log_activity(self, act):
        act['timestamp'] = self._now().isoformat()
        self.recent_acts.append(act)
        self.user_data[self._date_key()].append(act)
        self._prune_old_days()

    def _prune_old_days(self):
        if len(self.user_data) <= MAX_TRACKED_DAYS:
            return
        for stale in sorted(self.user_data)[:-MAX_TRACKED_DAYS]:
            del self.user_data[stale]

    def _recent_productivity_score(self):
        recent = list(self.recent_acts)[-10:]
        if not recent:
            return 0.5
        productive = sum(1 for a in recent if a.get('is_productive', False))
        return productive / len(recent)

    def _recent_app_switches(self, minutes):
        cutoff = self._now() - timedelta(minutes=minutes)
        apps = []
        for a in self.recent_acts:
            try:
                if datetime.fromisoformat(a['timestamp']) >= cutoff:
                    apps.append(a.get('app_name'))
            except (KeyError, TypeError, ValueError):
                continue
        return sum(a != b for a, b in zip(apps, apps[1:]))

    def _session_duration_mins(self):
        """Length of the current run of activities on the same app.

        Each reported activity counts as roughly one minute, matching the
        client's default polling cadence.
        """
        if not self.recent_acts:
            return 0
        current = self.recent_acts[-1].get('app_name')
        duration = 0
        for a in reversed(self.recent_acts):
            if a.get('app_name') != current:
                break
            duration += 1
        return duration

    def _context(self):
        return {
            'recent_productivity_score': self._recent_productivity_score(),
            'app_switches_last_hour': self._recent_app_switches(60),
            'current_session_duration': self._session_duration_mins(),
            'current_hour': self._now().hour,
        }

    # -------------- RL-powered analysis --------------
    def analyze(self, app_name, window_title, is_productive):
        context = self._context()
        activity = {
            'app_name': app_name,
            'window_title': window_title,
            'is_productive': is_productive,
            'duration_minutes': context['current_session_duration'],
        }
        return self.rl.process_activity(activity, context)

    def suggestions(self):
        return self.rl.get_suggestions(self._context())


api = FocusAssistantAPI()


def _json_body():
    """Parse a JSON body, returning (payload, error_response).

    request.get_json() raises on a missing or malformed body, which previously
    surfaced as an opaque 500.
    """
    payload = request.get_json(silent=True)
    if payload is None:
        return None, (jsonify({'status': 'error', 'error': 'Request body must be JSON'}), 400)
    if not isinstance(payload, dict):
        return None, (jsonify({'status': 'error', 'error': 'Request body must be a JSON object'}), 400)
    return payload, None


def _server_error(where, exc):
    """Log the detail, return a generic message.

    Echoing str(exc) to the caller leaked internal paths and state.
    """
    print(f"Error in {where}: {exc}")
    traceback.print_exc()
    return jsonify({'status': 'error', 'error': 'Internal server error'}), 500


@app.route('/health', methods=['GET'])
def health():
    return jsonify({'status': 'healthy', 'timestamp': datetime.now().isoformat()})


@app.route('/activity', methods=['POST'])
def log_activity():
    data, error = _json_body()
    if error:
        return error

    missing = [k for k in ('app_name', 'window_title', 'is_productive') if k not in data]
    if missing:
        return jsonify({'status': 'error', 'error': f"Missing field: {missing[0]}"}), 400

    try:
        api._log_activity(data)
        result = api.analyze(data['app_name'], data['window_title'], data['is_productive'])

        return jsonify({
            'status': 'success',
            'distraction_risk': result['distraction_risk'],
            'intervention_message': result['intervention_message'],
            'action_taken': result['action_taken'],
            'intervention_id': result['intervention_id'],
            'timestamp': datetime.now().isoformat(),
        })
    except Exception as exc:
        return _server_error('/activity', exc)


@app.route('/suggestions', methods=['GET'])
def get_suggestions():
    try:
        return jsonify({
            'status': 'success',
            'suggestions': api.suggestions(),
            'timestamp': datetime.now().isoformat(),
        })
    except Exception as exc:
        return _server_error('/suggestions', exc)


@app.route('/analytics', methods=['GET'])
def analytics():
    try:
        today = api._date_key()
        acts = api.user_data.get(today, [])
        productive = sum(1 for a in acts if a.get('is_productive', False))
        rate = (productive / len(acts)) * 100 if acts else 0

        app_counts = defaultdict(int)
        for a in acts:
            app_counts[a.get('app_name', 'unknown')] += 1

        top_apps = dict(sorted(app_counts.items(), key=lambda kv: kv[1], reverse=True)[:5])

        return jsonify({
            'status': 'success',
            'date': today,
            'total_activities': len(acts),
            'productivity_rate': round(rate, 1),
            'top_apps': top_apps,
            'recent_interventions': len(api.interventions),
            'timestamp': datetime.now().isoformat(),
        })
    except Exception as exc:
        return _server_error('/analytics', exc)


@app.route('/feedback', methods=['POST'])
def feedback():
    data, error = _json_body()
    if error:
        return error

    try:
        intervention_id = data.get('intervention_id')
        if intervention_id:
            api.rl.process_feedback(intervention_id, {
                'helpful': bool(data.get('helpful', False)),
                'user_action': data.get('action', 'none'),
                'productivity_change': data.get('productivity_change', 0),
            })

        api.interventions.append({'timestamp': datetime.now().isoformat(), **data})
        return jsonify({'status': 'success', 'message': 'Feedback recorded'})
    except Exception as exc:
        return _server_error('/feedback', exc)


@app.route('/insights', methods=['GET'])
def insights():
    try:
        return jsonify({
            'status': 'success',
            'insights': api.rl.get_model_insights(),
            'timestamp': datetime.now().isoformat(),
        })
    except Exception as exc:
        return _server_error('/insights', exc)


@app.route('/reset', methods=['POST'])
def reset_agent():
    """Discard everything the agent has learned.

    Requires an explicit {"confirm": true} body: this wipes the trained Q-table,
    and the next scheduled save then overwrites the model file on disk.
    """
    data = request.get_json(silent=True) or {}
    if data.get('confirm') is not True:
        return jsonify({
            'status': 'error',
            'error': 'Reset discards all learned state. Send {"confirm": true} to proceed.',
        }), 400

    try:
        api.rl.reset()
        api.user_data.clear()
        api.recent_acts.clear()
        api.interventions.clear()
        return jsonify({'status': 'success', 'message': 'RL agent has been reset'})
    except Exception as exc:
        return _server_error('/reset', exc)


if __name__ == '__main__':
    print(f"Starting AI Focus Assistant API on http://{config.HOST}:{config.PORT} (debug={config.DEBUG})")
    # use_reloader is tied to debug explicitly: the client tracks a single PID and
    # cannot shut down a reloader's child process, which leaves the port held.
    app.run(
        host=config.HOST,
        port=config.PORT,
        debug=config.DEBUG,
        use_reloader=config.DEBUG,
        threaded=True,
    )
