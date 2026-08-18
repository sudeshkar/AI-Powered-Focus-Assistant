# rl_agent.py - Integration service wiring EnhancedFocusRLAgent to the Flask API.
from datetime import datetime

import config
from enhanced_rl_agent import EnhancedFocusRLAgent

# An intervention the user never responds to would otherwise be held forever,
# because process_feedback is the only thing that removes entries.
MAX_PENDING_INTERVENTIONS = 200

# Risk score reported to the client for each action the agent can choose.
ACTION_RISK = {
    'no_intervention': 0.1,
    'micro_break_5min': 0.4,
    'gentle_refocus': 0.3,
    'task_prioritization': 0.7,
    'environment_optimization': 0.6,
    'energy_boost_suggestion': 0.5,
    'deep_work_mode': 0.2,
    'collaborative_focus': 0.3,
    'mindfulness_prompt': 0.3,
    'productivity_gamification': 0.2,
    'adaptive_blocking': 0.9,
    'cognitive_load_reduction': 0.5,
    'flow_state_induction': 0.2,
}


class RLIntegrationService:
    """Adapts EnhancedFocusRLAgent to the shapes the HTTP API exposes."""

    def __init__(self):
        self.rl_agent = EnhancedFocusRLAgent()
        self.current_interventions = {}
        self._feedback_since_save = 0

    def reset(self):
        self.rl_agent.reset()
        self.current_interventions.clear()
        self._feedback_since_save = 0
        print("RLIntegrationService reset.")

    def process_activity(self, activity_data, context_data):
        """Score an activity and, when warranted, produce an intervention."""
        state = self.rl_agent.get_enhanced_state_vector(activity_data, context_data)
        action = self.rl_agent.select_intelligent_action(state)
        intervention_message = self.rl_agent.get_personalized_intervention_message(
            action, state, {'current_app': activity_data.get('app_name', ''), **context_data}
        )

        intervention_id = None
        if intervention_message:
            intervention_id = f"int_{int(datetime.now().timestamp() * 1000)}"
            self.current_interventions[intervention_id] = {
                'state': state,
                'action': action,
                'timestamp': datetime.now(),
                'activity': activity_data,
            }
            self._evict_stale_interventions()

        return {
            'distraction_risk': ACTION_RISK.get(action, 0.5),
            'intervention_message': intervention_message,
            'action_taken': action,
            'intervention_id': intervention_id,
        }

    def _evict_stale_interventions(self):
        """Drop the oldest pending interventions once the cap is exceeded."""
        overflow = len(self.current_interventions) - MAX_PENDING_INTERVENTIONS
        if overflow <= 0:
            return
        oldest = sorted(self.current_interventions,
                        key=lambda k: self.current_interventions[k]['timestamp'])
        for key in oldest[:overflow]:
            del self.current_interventions[key]

    def process_feedback(self, intervention_id, feedback_data):
        """Attribute the user's response to the intervention that prompted it."""
        intervention = self.current_interventions.pop(intervention_id, None)
        if intervention is None:
            # Unknown or already-consumed id: nothing to attribute the reward to.
            print(f"Ignoring feedback for unknown intervention {intervention_id!r}")
            return False

        elapsed = (datetime.now() - intervention['timestamp']).total_seconds() / 60
        feedback_data['response_time_minutes'] = elapsed

        self.rl_agent.learn_from_enhanced_feedback(
            intervention['state'],
            intervention['action'],
            feedback_data,
            intervention['activity'],
        )

        # Count our own saves rather than reading len(feedback_history), which is a
        # bounded deque and so stops advancing once it is full.
        self._feedback_since_save += 1
        if self._feedback_since_save >= config.MODEL_SAVE_INTERVAL:
            self._feedback_since_save = 0
            self.rl_agent.save_enhanced_model()

        return True

    def get_suggestions(self, context_data=None):
        """Aggregate behavioural patterns.

        These are whole-history aggregates, not specific to the current moment;
        context_data is accepted for API symmetry and intentionally unused.
        """
        return self.rl_agent.get_advanced_model_insights()['behavioral_patterns']

    def get_model_insights(self):
        return self.rl_agent.get_advanced_model_insights()
