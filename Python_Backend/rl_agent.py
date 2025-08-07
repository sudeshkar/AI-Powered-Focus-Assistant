# rl_agent.py - Reinforcement Learning Agent for Focus Assistant
import numpy as np
import json
from datetime import datetime, timedelta
from collections import defaultdict, deque
import pickle
import os

class FocusRLAgent:
    """
    Reinforcement Learning Agent that learns user distraction patterns
    and provides personalized interventions
    """
    
    def __init__(self, learning_rate=0.1, discount_factor=0.9, epsilon=0.3):
        # RL Parameters
        self.learning_rate = learning_rate
        self.discount_factor = discount_factor
        self.epsilon = epsilon  # Exploration rate
        
        # State-Action Value Table (Q-Table)
        self.q_table = defaultdict(lambda: defaultdict(float))
        
        # User behavior tracking
        self.user_sessions = []
        self.intervention_history = []
        self.feedback_history = []
        
        # Pattern recognition
        self.distraction_patterns = {}
        self.productive_patterns = {}
        
        # State features
        self.state_features = [
            'current_app_category',
            'time_of_day_bin',
            'day_of_week',
            'session_duration_bin',
            'recent_productivity_score',
            'app_switch_frequency'
        ]
        
        # Actions the agent can take
        self.actions = [
            'no_intervention',
            'gentle_reminder',
            'break_suggestion',
            'task_redirect',
            'block_suggestion',
            'productivity_tip'
        ]
        
        # Load previous learning if exists
        self.load_model()
    
    def get_state_vector(self, current_activity, context):
        """
        Convert current situation into state vector for RL
        """
        state = {}
        
        # App category
        app_category = self.categorize_app(current_activity.get('app_name', ''))
        state['current_app_category'] = app_category
        
        # Time of day (4 bins: morning, afternoon, evening, night)
        hour = datetime.now().hour
        if 6 <= hour < 12:
            time_bin = 'morning'
        elif 12 <= hour < 17:
            time_bin = 'afternoon'
        elif 17 <= hour < 22:
            time_bin = 'evening'
        else:
            time_bin = 'night'
        state['time_of_day_bin'] = time_bin
        
        # Day of week
        state['day_of_week'] = datetime.now().strftime('%A').lower()
        
        # Session duration bin
        duration_min = current_activity.get('duration_minutes', 0)
        if duration_min < 5:
            duration_bin = 'short'
        elif duration_min < 20:
            duration_bin = 'medium'
        else:
            duration_bin = 'long'
        state['session_duration_bin'] = duration_bin
        
        # Recent productivity score
        recent_score = context.get('recent_productivity_score', 0.5)
        if recent_score < 0.3:
            prod_bin = 'low'
        elif recent_score < 0.7:
            prod_bin = 'medium'
        else:
            prod_bin = 'high'
        state['recent_productivity_score'] = prod_bin
        
        # App switching frequency
        switch_freq = context.get('app_switches_last_hour', 0)
        if switch_freq < 3:
            switch_bin = 'low'
        elif switch_freq < 8:
            switch_bin = 'medium'
        else:
            switch_bin = 'high'
        state['app_switch_frequency'] = switch_bin
        
        return state
    
    def categorize_app(self, app_name):
        """Categorize application into productivity type"""
        productivity_apps = {
            'development': ['vscode', 'visual studio', 'pycharm', 'atom', 'sublime'],
            'design': ['photoshop', 'illustrator', 'figma', 'sketch', 'canva'],
            'productivity': ['word', 'excel', 'powerpoint', 'notion', 'todoist'],
            'communication': ['outlook', 'teams', 'slack', 'zoom', 'skype'],
            'research': ['chrome', 'firefox', 'edge'],  # Can be productive or distracting
            'entertainment': ['youtube', 'netflix', 'spotify', 'steam', 'discord'],
            'social': ['facebook', 'twitter', 'instagram', 'reddit', 'tiktok']
        }
        
        app_lower = app_name.lower()
        for category, apps in productivity_apps.items():
            if any(app in app_lower for app in apps):
                return category
        return 'unknown'
    
    def state_to_key(self, state):
        """Convert state dict to hashable key for Q-table"""
        return tuple(sorted(state.items()))
    
    def select_action(self, state):
        """
        Select action using epsilon-greedy policy
        """
        state_key = self.state_to_key(state)
        
        # Exploration vs Exploitation
        if np.random.random() < self.epsilon:
            # Random action (exploration)
            action = np.random.choice(self.actions)
        else:
            # Best action based on Q-values (exploitation)
            q_values = self.q_table[state_key]
            if q_values:
                action = max(q_values.items(), key=lambda x: x[1])[0]
            else:
                # If no Q-values exist, use rule-based fallback
                action = self.rule_based_fallback(state)
        
        return action
    
    def rule_based_fallback(self, state):
        """
        Rule-based action selection for new states
        """
        app_category = state['current_app_category']
        duration_bin = state['session_duration_bin']
        productivity_score = state['recent_productivity_score']
        
        # High-risk scenarios
        if app_category in ['entertainment', 'social'] and duration_bin == 'long':
            return 'task_redirect'
        
        # Medium-risk scenarios
        if app_category == 'research' and productivity_score == 'low':
            return 'gentle_reminder'
        
        # Break suggestions
        if duration_bin == 'long' and productivity_score == 'high':
            return 'break_suggestion'
        
        # Default: no intervention for productive activities
        if app_category in ['development', 'productivity'] and productivity_score != 'low':
            return 'no_intervention'
        
        return 'gentle_reminder'
    
    def get_intervention_message(self, action, state, context):
        """
        Generate specific intervention message based on action
        """
        app_name = context.get('current_app', 'this app')
        
        messages = {
            'no_intervention': None,
            'gentle_reminder': [
                f"💡 You're doing well! Keep focused on your current task.",
                f"🎯 Great progress so far. Stay in the zone!",
                f"⭐ You've maintained good focus. Keep it up!"
            ],
            'break_suggestion': [
                f"⏰ You've been focused for a while. Consider a 5-minute break!",
                f"🚶‍♂️ Time for a quick stretch? You've earned it!",
                f"💧 Grab some water and take a short breather.",
                f"👀 Give your eyes a rest - look away from the screen for a moment."
            ],
            'task_redirect': [
                f"🎯 Time to switch back to your main task?",
                f"📋 What's your top priority right now?",
                f"⚡ Let's channel this energy into your important work!",
                f"🔄 Ready to tackle that priority task?"
            ],
            'block_suggestion': [
                f"⛔ Consider temporarily blocking {app_name} to stay focused.",
                f"🚫 This might be a good time to close distracting tabs.",
                f"🔒 Block distractions for the next 25 minutes?"
            ],
            'productivity_tip': [
                f"💡 Try the Pomodoro technique: 25 min work, 5 min break!",
                f"🎯 Set a specific goal for the next 15 minutes.",
                f"📝 Write down your current task to stay accountable.",
                f"🏆 Reward yourself after completing this task!",
                f"⏰ Use a timer to create urgency and focus."
            ]
        }
        
        if action in messages and messages[action]:
            return np.random.choice(messages[action])
        return None
    
    def learn_from_feedback(self, state, action, feedback_data):
        """
        Update Q-values based on user feedback
        """
        state_key = self.state_to_key(state)
        
        # Calculate reward based on feedback
        reward = self.calculate_reward(feedback_data)
        
        # Q-Learning update
        current_q = self.q_table[state_key][action]
        
        # For simplicity, we'll use a basic update without next state
        # (In full RL, we'd consider the next state's max Q-value)
        new_q = current_q + self.learning_rate * (reward - current_q)
        self.q_table[state_key][action] = new_q
        
        # Store feedback for pattern analysis
        feedback_entry = {
            'state': state,
            'action': action,
            'reward': reward,
            'feedback': feedback_data,
            'timestamp': datetime.now().isoformat()
        }
        self.feedback_history.append(feedback_entry)
        
        # Decay epsilon (reduce exploration over time)
        self.epsilon = max(0.1, self.epsilon * 0.995)
        
        print(f"🧠 RL Update: Action '{action}' reward={reward:.2f}, new Q={new_q:.2f}")
    
    def calculate_reward(self, feedback_data):
        """
        Calculate reward based on user feedback and outcomes
        """
        base_reward = 0.0
        
        # Direct user feedback
        if feedback_data.get('helpful', False):
            base_reward += 1.0
        elif feedback_data.get('helpful') == False:
            base_reward -= 0.5
        
        # User action after intervention
        action_taken = feedback_data.get('user_action', 'none')
        action_rewards = {
            'acted': 1.5,        # User took suggested action
            'dismissed': -0.3,   # User dismissed intervention
            'ignored': 0.0,      # Neutral - user ignored
            'none': 0.0
        }
        base_reward += action_rewards.get(action_taken, 0.0)
        
        # Productivity improvement
        productivity_change = feedback_data.get('productivity_change', 0)
        base_reward += productivity_change * 0.5
        
        # Time since intervention (faster positive response = better)
        response_time_minutes = feedback_data.get('response_time_minutes', 10)
        if base_reward > 0 and response_time_minutes < 5:
            base_reward += 0.2  # Quick positive response bonus
        
        return np.clip(base_reward, -2.0, 2.0)  # Limit reward range
    
    def analyze_patterns(self):
        """
        Analyze user behavior patterns from feedback history
        """
        if len(self.feedback_history) < 10:
            return "Need more data to identify patterns."
        
        patterns = {
            'time_preferences': defaultdict(list),
            'app_preferences': defaultdict(list),
            'intervention_preferences': defaultdict(list)
        }
        
        for feedback in self.feedback_history[-50:]:  # Last 50 feedbacks
            state = feedback['state']
            reward = feedback['reward']
            action = feedback['action']
            
            # Time-based patterns
            patterns['time_preferences'][state['time_of_day_bin']].append(reward)
            
            # App-based patterns
            patterns['app_preferences'][state['current_app_category']].append(reward)
            
            # Intervention type preferences
            patterns['intervention_preferences'][action].append(reward)
        
        # Calculate averages and insights
        insights = []
        
        # Best times for interventions
        time_scores = {time: np.mean(rewards) for time, rewards in patterns['time_preferences'].items()}
        best_time = max(time_scores.items(), key=lambda x: x[1])
        insights.append(f"🕐 Most receptive to interventions during: {best_time[0]} (score: {best_time[1]:.2f})")
        
        # Most effective intervention types
        intervention_scores = {action: np.mean(rewards) for action, rewards in patterns['intervention_preferences'].items() if len(rewards) > 2}
        if intervention_scores:
            best_intervention = max(intervention_scores.items(), key=lambda x: x[1])
            insights.append(f"🎯 Most effective intervention: {best_intervention[0]} (score: {best_intervention[1]:.2f})")
        
        # App categories needing attention
        app_scores = {app: np.mean(rewards) for app, rewards in patterns['app_preferences'].items()}
        problematic_apps = [app for app, score in app_scores.items() if score < -0.2]
        if problematic_apps:
            insights.append(f"⚠️ Apps needing attention: {', '.join(problematic_apps)}")
        
        return insights
    
    def get_personalized_suggestions(self, current_context):
        """
        Generate personalized suggestions based on learned patterns
        """
        suggestions = []
        
        # Base suggestions from pattern analysis
        patterns = self.analyze_patterns()
        if isinstance(patterns, list):
            suggestions.extend(patterns[:2])  # Top 2 insights
        
        # Context-specific suggestions
        current_hour = datetime.now().hour
        
        if current_hour in [14, 15, 16]:  # Afternoon productivity dip
            suggestions.append("☕ Consider a coffee break or short walk to boost afternoon energy!")
        
        # Based on Q-table learning
        if len(self.q_table) > 5:
            # Find most successful state-action combinations
            successful_actions = []
            for state_key, actions in self.q_table.items():
                best_action = max(actions.items(), key=lambda x: x[1])
                if best_action[1] > 0.5:  # Good Q-value
                    successful_actions.append(best_action[0])
            
            if successful_actions:
                most_common = max(set(successful_actions), key=successful_actions.count)
                if most_common != 'no_intervention':
                    suggestions.append(f"🧠 Based on your patterns, {most_common.replace('_', ' ')} works well for you!")
        
        # Fallback suggestions if no patterns yet
        if len(suggestions) == 0:
            suggestions = [
                "💡 Keep using the app to build your personalized productivity profile!",
                "🎯 Try different focus techniques to see what works best for you.",
                "📊 Your AI assistant is learning your patterns in real-time."
            ]
        
        return suggestions[:3]  # Return top 3 suggestions
    
    def save_model(self):
        """Save the trained model and data"""
        try:
            model_dir = os.path.join(os.path.dirname(__file__), 'models')
            os.makedirs(model_dir, exist_ok=True)
            
            model_data = {
                'q_table': dict(self.q_table),
                'feedback_history': self.feedback_history[-1000:],  # Keep last 1000
                'learning_params': {
                    'learning_rate': self.learning_rate,
                    'discount_factor': self.discount_factor,
                    'epsilon': self.epsilon
                },
                'timestamp': datetime.now().isoformat()
            }
            
            model_path = os.path.join(model_dir, 'focus_rl_model.json')
            with open(model_path, 'w') as f:
                json.dump(model_data, f, indent=2)
                
            print(f"💾 RL Model saved: {len(self.q_table)} states, {len(self.feedback_history)} feedback entries")
            
        except Exception as e:
            print(f"❌ Failed to save model: {e}")
    
    def load_model(self):
        """Load previously trained model"""
        try:
            model_path = os.path.join(os.path.dirname(__file__), 'models', 'focus_rl_model.json')
            
            if os.path.exists(model_path):
                with open(model_path, 'r') as f:
                    model_data = json.load(f)
                
                # Restore Q-table
                for state_key, actions in model_data.get('q_table', {}).items():
                    # Convert string keys back to tuples
                    if isinstance(state_key, str):
                        state_key = eval(state_key)  # Careful: only for trusted data
                    self.q_table[state_key] = defaultdict(float, actions)
                
                # Restore feedback history
                self.feedback_history = model_data.get('feedback_history', [])
                
                # Restore learning parameters
                params = model_data.get('learning_params', {})
                self.epsilon = params.get('epsilon', self.epsilon)
                
                print(f"📂 RL Model loaded: {len(self.q_table)} states, {len(self.feedback_history)} feedback entries")
                
        except Exception as e:
            print(f"⚠️ Could not load previous model: {e}")
            print("🆕 Starting with fresh RL model")
    
    def get_model_stats(self):
        """Get statistics about the current model"""
        total_states = len(self.q_table)
        total_feedbacks = len(self.feedback_history)
        
        # Calculate average Q-values
        all_q_values = []
        for state_actions in self.q_table.values():
            all_q_values.extend(state_actions.values())
        
        avg_q_value = np.mean(all_q_values) if all_q_values else 0.0
        
        # Recent performance
        recent_rewards = []
        if len(self.feedback_history) > 10:
            recent_rewards = [f['reward'] for f in self.feedback_history[-20:]]
        
        avg_recent_reward = np.mean(recent_rewards) if recent_rewards else 0.0
        
        return {
            'total_states': total_states,
            'total_feedbacks': total_feedbacks,
            'average_q_value': round(avg_q_value, 3),
            'average_recent_reward': round(avg_recent_reward, 3),
            'exploration_rate': round(self.epsilon, 3),
            'learning_progress': 'Good' if avg_recent_reward > 0.2 else 'Learning' if total_feedbacks > 5 else 'Starting'
        }


# Integration class for Flask app
class RLIntegrationService:
    """
    Service to integrate RL agent with Flask API
    """
    
    def __init__(self):
        self.rl_agent = FocusRLAgent()
        self.current_interventions = {}  # Track active interventions
        
    def process_activity(self, activity_data, context_data):
        """
        Process incoming activity and return RL-based intervention
        """
        # Get current state
        state = self.rl_agent.get_state_vector(activity_data, context_data)
        
        # Select action using RL agent
        action = self.rl_agent.select_action(state)
        
        # Generate intervention message
        intervention_message = self.rl_agent.get_intervention_message(
            action, state, {
                'current_app': activity_data.get('app_name', ''),
                **context_data
            }
        )
        
        # Calculate risk score (for compatibility with existing API)
        risk_score = self.calculate_risk_score(action, state)
        
        # Store intervention for feedback tracking
        if intervention_message:
            intervention_id = f"{datetime.now().timestamp():.0f}"
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
            'intervention_id': intervention_id if intervention_message else None
        }
    
    def calculate_risk_score(self, action, state):
        """Convert RL action to risk score for API compatibility"""
        action_risk_mapping = {
            'no_intervention': 0.1,
            'gentle_reminder': 0.3,
            'break_suggestion': 0.4,
            'task_redirect': 0.7,
            'block_suggestion': 0.9,
            'productivity_tip': 0.2
        }
        return action_risk_mapping.get(action, 0.5)
    
    def process_feedback(self, intervention_id, feedback_data):
        """
        Process user feedback and update RL model
        """
        if intervention_id in self.current_interventions:
            intervention = self.current_interventions[intervention_id]
            
            # Add timing information
            response_time = (datetime.now() - intervention['timestamp']).total_seconds() / 60
            feedback_data['response_time_minutes'] = response_time
            
            # Learn from feedback
            self.rl_agent.learn_from_feedback(
                intervention['state'],
                intervention['action'],
                feedback_data
            )
            
            # Clean up old interventions
            del self.current_interventions[intervention_id]
            
            # Save model periodically
            if len(self.rl_agent.feedback_history) % 10 == 0:
                self.rl_agent.save_model()
    
    def get_suggestions(self, context_data):
        """Get personalized suggestions from RL agent"""
        return self.rl_agent.get_personalized_suggestions(context_data)
    
    def get_model_insights(self):
        """Get insights about the current RL model"""
        stats = self.rl_agent.get_model_stats()
        patterns = self.rl_agent.analyze_patterns()
        
        return {
            'model_stats': stats,
            'behavior_patterns': patterns if isinstance(patterns, list) else [patterns],
            'recommendations': self.rl_agent.get_personalized_suggestions({})
        }