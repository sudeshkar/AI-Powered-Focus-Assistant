# rl_agent.py - Integration service for EnhancedFocusRLAgent
from enhanced_rl_agent import EnhancedFocusRLAgent
from datetime import datetime
from collections import defaultdict

class RLIntegrationService:
    """Service to integrate EnhancedFocusRLAgent with Flask API"""
    
    def __init__(self):
        self.rl_agent = EnhancedFocusRLAgent()
        self.current_interventions = {}
    
    def reset(self):
        """Reset the internal RL agent and interventions"""
        self.rl_agent.reset()
        self.current_interventions.clear()
        print("🧹 RLIntegrationService fully reset.")
    
    def process_activity(self, activity_data, context_data):
        """Process incoming activity and return RL-based intervention"""
        state = self.rl_agent.get_enhanced_state_vector(activity_data, context_data)
        action = self.rl_agent.select_intelligent_action(state)
        intervention_message = self.rl_agent.get_personalized_intervention_message(
            action, state, {
                'current_app': activity_data.get('app_name', ''),
                **context_data
            }
        )
        risk_score = self._calculate_risk_score(action, state)
        intervention_id = None
        if intervention_message:
            intervention_id = f"int_{int(datetime.now().timestamp() * 1000)}"
            self.current_interventions[intervention_id] = {
                'state': state,
                'action': action,
                'timestamp': datetime.now(),
                'activity': activity_data
            }
        return {
            'distraction_risk': risk_score,
            'intervention_message': intervention_message,
            'action_taken': action,
            'intervention_id': intervention_id
        }
    
    def _calculate_risk_score(self, action, state):
        """Convert RL action to risk score for API compatibility"""
        action_risk_mapping = {
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
            'flow_state_induction': 0.2
        }
        return action_risk_mapping.get(action, 0.5)
    
    def process_feedback(self, intervention_id, feedback_data):
        """Process user feedback and update RL model"""
        if intervention_id in self.current_interventions:
            intervention = self.current_interventions[intervention_id]
            response_time = (datetime.now() - intervention['timestamp']).total_seconds() / 60
            feedback_data['response_time_minutes'] = response_time
            self.rl_agent.learn_from_enhanced_feedback(
                intervention['state'],
                intervention['action'],
                feedback_data,
                intervention['activity']
            )
            del self.current_interventions[intervention_id]
            if len(self.rl_agent.feedback_history) % 10 == 0:
                self.rl_agent.save_enhanced_model()
    
    def get_suggestions(self, context_data):
        """Get personalized suggestions from RL agent"""
        return self.rl_agent.get_advanced_model_insights()['behavioral_patterns']
    
    def get_model_insights(self):
        """Get insights about the current RL model"""
        return self.rl_agent.get_advanced_model_insights()