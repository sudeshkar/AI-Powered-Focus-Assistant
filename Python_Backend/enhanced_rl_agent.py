# enhanced_rl_agent.py - Advanced RL Agent with Sophisticated Learning
import numpy as np
import json
import pickle
import os
from datetime import datetime, timedelta
from collections import defaultdict, deque
import hashlib
from sklearn.preprocessing import StandardScaler
from sklearn.cluster import KMeans
import threading
import time

class EnhancedFocusRLAgent:
    def __init__(self, learning_rate=0.1, discount_factor=0.95, epsilon=0.2):
        # Core RL Parameters
        self.learning_rate = learning_rate
        self.discount_factor = discount_factor
        self.epsilon = epsilon
        self.epsilon_decay = 0.999
        self.epsilon_min = 0.05
        
        # Advanced Q-table with string keys for JSON compatibility
        self.q_table = {}  # Regular dict to avoid lambda serialization issues
        self.state_visits = defaultdict(int)
        self.action_success_rates = defaultdict(lambda: defaultdict(list))
        
        # Multi-level Learning Systems
        self.implicit_learner = ImplicitLearningSystem()
        self.pattern_analyzer = UserPatternAnalyzer()
        self.context_predictor = ContextPredictor()
        
        # Enhanced Data Tracking
        self.user_sessions = deque(maxlen=1000)
        self.intervention_history = deque(maxlen=2000)
        self.feedback_history = deque(maxlen=1500)
        self.behavioral_metrics = BehavioralMetrics()
        
        # Personalization Engine
        self.user_profile = UserProfile()
        self.adaptive_thresholds = AdaptiveThresholds()
        
        # Time-based Learning
        self.temporal_patterns = TemporalPatterns()
        self.circadian_optimizer = CircadianOptimizer()
        
        # Advanced State Features
        self.state_features = [
            'current_app_category', 'time_of_day_bin', 'day_of_week',
            'session_duration_bin', 'recent_productivity_score',
            'app_switch_frequency', 'cognitive_load_estimate',
            'focus_momentum', 'distraction_susceptibility',
            'energy_level_estimate', 'task_complexity',
            'social_context', 'environmental_factors'
        ]
        
        # Expanded Action Space
        self.actions = [
            'no_intervention', 'micro_break_5min', 'gentle_refocus',
            'task_prioritization', 'environment_optimization',
            'energy_boost_suggestion', 'deep_work_mode',
            'collaborative_focus', 'mindfulness_prompt',
            'productivity_gamification', 'adaptive_blocking',
            'cognitive_load_reduction', 'flow_state_induction'
        ]
        
        # Load and initialize
        self.load_model()
        self._start_background_learning()
    
    def _start_background_learning(self):
        """Start background thread for continuous learning"""
        def background_worker():
            while True:
                time.sleep(300)  # Every 5 minutes
                self._background_analysis()
                
        thread = threading.Thread(target=background_worker, daemon=True)
        thread.start()
    
    def _background_analysis(self):
        """Continuous background analysis and learning"""
        try:
            self.pattern_analyzer.update_patterns(list(self.user_sessions))
            self.circadian_optimizer.update_preferences(self.feedback_history)
            self.adaptive_thresholds.adjust_based_on_performance(self.feedback_history)
            self.context_predictor.train_on_recent_data(list(self.user_sessions)[-100:])
        except Exception as e:
            print(f"Background analysis error: {e}")
    
    def _get_time_bin(self, hour):
        """Convert hour to time bin"""
        if 6 <= hour < 9:
            return 'early_morning'
        elif 9 <= hour < 12:
            return 'morning'
        elif 12 <= hour < 14:
            return 'midday'
        elif 14 <= hour < 17:
            return 'afternoon'
        elif 17 <= hour < 20:
            return 'evening'
        elif 20 <= hour < 23:
            return 'night'
        else:
            return 'late_night'
    
    def _get_week_phase(self, dt):
        """Get current week phase"""
        return 'weekday' if dt.weekday() < 5 else 'weekend'
    
    def _get_duration_bin(self, duration_min):
        """Bin session duration"""
        if duration_min < 2:
            return 'very_short'
        elif duration_min < 10:
            return 'short'
        elif duration_min < 30:
            return 'medium'
        elif duration_min < 60:
            return 'long'
        else:
            return 'very_long'
    
    def _bin_score(self, score):
        """Bin productivity score"""
        if score < 0.3:
            return 'low'
        elif score < 0.7:
            return 'medium'
        else:
            return 'high'
    
    def _bin_switches(self, switches):
        """Bin app switches"""
        if switches < 3:
            return 'low'
        elif switches < 8:
            return 'medium'
        else:
            return 'high'
    
    def _basic_app_categorization(self, app_name):
        """Basic app categorization"""
        categories = {
            'development': ['vscode', 'pycharm', 'intellij', 'sublime', 'atom', 'vim'],
            'productivity': ['word', 'excel', 'powerpoint', 'notion', 'todoist', 'asana'],
            'communication': ['outlook', 'teams', 'slack', 'zoom', 'discord'],
            'research': ['chrome', 'firefox', 'edge', 'safari'],
            'entertainment': ['youtube', 'netflix', 'spotify', 'steam', 'twitch'],
            'social': ['facebook', 'twitter', 'instagram', 'reddit', 'tiktok'],
            'design': ['photoshop', 'illustrator', 'figma', 'sketch', 'canva']
        }
        app_lower = app_name.lower()
        for category, apps in categories.items():
            if any(app in app_lower for app in apps):
                return category
        return 'other'
    
    def _calculate_session_momentum(self):
        """Calculate current session momentum"""
        if len(self.user_sessions) < 3:
            return 'neutral'
        recent_sessions = list(self.user_sessions)[-5:]
        productivity_trend = [s.get('productivity_score', 0.5) for s in recent_sessions]
        if len(productivity_trend) >= 2:
            trend = np.polyfit(range(len(productivity_trend)), productivity_trend, 1)[0]
            if trend > 0.1:
                return 'increasing'
            elif trend < -0.1:
                return 'decreasing'
        return 'stable'
    
    def _get_productivity_trend(self):
        """Calculate productivity trend"""
        if len(self.feedback_history) < 5:
            return 'neutral'
        recent_rewards = [f.get('reward', 0) for f in list(self.feedback_history)[-10:]]
        if len(recent_rewards) >= 3:
            trend = np.polyfit(range(len(recent_rewards)), recent_rewards, 1)[0]
            if trend > 0.05:
                return 'improving'
            elif trend < -0.05:
                return 'declining'
        return 'stable'
    
    def _calculate_focus_momentum(self):
        """Calculate focus momentum"""
        if len(self.user_sessions) < 2:
            return 'neutral'
        recent_sessions = list(self.user_sessions)[-3:]
        focus_scores = []
        for session in recent_sessions:
            duration = session.get('duration_minutes', 0)
            switches = session.get('app_switches', 0)
            focus_score = min(duration / 30, 1.0) - min(switches / 10, 1.0)
            focus_scores.append(focus_score)
        avg_focus = np.mean(focus_scores)
        if avg_focus > 0.3:
            return 'high'
        elif avg_focus < -0.3:
            return 'low'
        return 'medium'
    
    def _estimate_cognitive_load(self, activity):
        """Estimate cognitive load based on current activity"""
        app_category = self._basic_app_categorization(activity.get('app_name', ''))
        window_title = activity.get('window_title', '').lower()
        base_loads = {
            'development': 0.8,
            'design': 0.7,
            'productivity': 0.6,
            'research': 0.5,
            'communication': 0.4,
            'entertainment': 0.2,
            'social': 0.3
        }
        base_load = base_loads.get(app_category, 0.5)
        if any(keyword in window_title for keyword in ['debug', 'error', 'problem', 'complex', 'algorithm']):
            base_load += 0.2
        elif any(keyword in window_title for keyword in ['tutorial', 'simple', 'basic', 'easy']):
            base_load -= 0.2
        if base_load > 0.8:
            return 'high'
        elif base_load > 0.5:
            return 'medium'
        else:
            return 'low'
    
    def _calculate_distraction_susceptibility(self):
        """Calculate current distraction susceptibility"""
        if len(self.feedback_history) < 5:
            return 'medium'
        recent_interventions = list(self.feedback_history)[-10:]
        negative_responses = sum(1 for f in recent_interventions if f.get('reward', 0) < -0.1)
        susceptibility_ratio = negative_responses / len(recent_interventions)
        if susceptibility_ratio > 0.6:
            return 'high'
        elif susceptibility_ratio < 0.3:
            return 'low'
        return 'medium'
    
    def _estimate_task_complexity(self, activity):
        """Estimate current task complexity"""
        app_category = self._basic_app_categorization(activity.get('app_name', ''))
        duration = activity.get('duration_minutes', 0)
        complexity_scores = {
            'development': 0.8,
            'design': 0.7,
            'productivity': 0.5,
            'research': 0.6,
            'communication': 0.3,
            'entertainment': 0.1,
            'social': 0.2
        }
        base_complexity = complexity_scores.get(app_category, 0.5)
        if duration > 60:
            base_complexity += 0.2
        elif duration < 5:
            base_complexity -= 0.2
        if base_complexity > 0.7:
            return 'high'
        elif base_complexity > 0.4:
            return 'medium'
        return 'low'
    
    def _assess_environment(self, context):
        """Assess environmental factors"""
        hour = datetime.now().hour
        if 9 <= hour <= 17:
            return 'office_hours'
        elif 17 <= hour <= 22:
            return 'evening'
        else:
            return 'off_hours'
    
    def get_enhanced_state_vector(self, current_activity, context):
        """Create comprehensive state representation"""
        state = {}
        now = datetime.now()
        state['current_app_category'] = self.categorize_app_enhanced(
            current_activity.get('app_name', ''),
            current_activity.get('window_title', '')
        )
        state['time_of_day_bin'] = self._get_time_bin(now.hour)
        state['day_of_week'] = now.strftime('%A').lower()
        state['week_phase'] = self._get_week_phase(now)
        duration_min = current_activity.get('duration_minutes', 0)
        state['session_duration_bin'] = self._get_duration_bin(duration_min)
        state['session_momentum'] = self._calculate_session_momentum()
        state['recent_productivity_score'] = self._bin_score(
            context.get('recent_productivity_score', 0.5)
        )
        state['productivity_trend'] = self._get_productivity_trend()
        state['app_switch_frequency'] = self._bin_switches(
            context.get('app_switches_last_hour', 0)
        )
        state['focus_momentum'] = self._calculate_focus_momentum()
        state['cognitive_load_estimate'] = self._estimate_cognitive_load(current_activity)
        state['energy_level_estimate'] = self.circadian_optimizer.estimate_energy_level(now)
        state['circadian_phase'] = self.circadian_optimizer.get_circadian_phase(now)
        state['distraction_susceptibility'] = self._calculate_distraction_susceptibility()
        state['task_complexity'] = self._estimate_task_complexity(current_activity)
        state['social_context'] = context.get('social_context', 'solo')
        state['environmental_factors'] = self._assess_environment(context)
        return state
    
    def categorize_app_enhanced(self, app_name, window_title):
        """Enhanced app categorization with window title context"""
        base_category = self._basic_app_categorization(app_name)
        if base_category == 'research':
            if any(term in window_title.lower() for term in 
                   ['stackoverflow', 'documentation', 'tutorial', 'learn']):
                return 'learning_research'
            elif any(term in window_title.lower() for term in 
                     ['youtube', 'entertainment', 'funny', 'meme']):
                return 'entertainment_research'
        if base_category == 'communication':
            if any(term in window_title.lower() for term in 
                   ['meeting', 'standup', 'review', 'discussion']):
                return 'productive_communication'
            elif any(term in window_title.lower() for term in 
                     ['chat', 'casual', 'break', 'lunch']):
                return 'social_communication'
        return base_category
    
    def state_to_key(self, state):
        """Convert state dict to string key for JSON compatibility"""
        return json.dumps(state, sort_keys=True)
    
    def select_intelligent_action(self, state):
        """Enhanced action selection with multiple decision layers"""
        state_key = self.state_to_key(state)
        if state_key not in self.q_table:
            self.q_table[state_key] = {action: 0.0 for action in self.actions}
        self.state_visits[state_key] += 1
        
        emergency_action = self._check_emergency_interventions(state)
        if emergency_action:
            return emergency_action
        
        predicted_context = self.context_predictor.predict_next_context(state)
        
        if np.random.random() < self.epsilon:
            action = self._intelligent_exploration(state, predicted_context)
        else:
            action = self._ensemble_action_selection(state, state_key)
        
        action = self._validate_and_refine_action(action, state, predicted_context)
        self.implicit_learner.learn_from_selection(state, action, predicted_context)
        return action
    
    def _check_emergency_interventions(self, state):
        """Check for high-priority intervention needs"""
        if state.get('cognitive_load_estimate') == 'high' and state.get('energy_level_estimate') == 'low':
            return 'energy_boost_suggestion'
        if (state.get('current_app_category') in ['entertainment', 'social'] and 
            state.get('session_duration_bin') in ['long', 'very_long'] and
            state.get('distraction_susceptibility') == 'high'):
            return 'gentle_refocus'
        return None
    
    def _intelligent_exploration(self, state, predicted_context):
        """Intelligent exploration (not purely random)"""
        similar_states = [k for k in self.q_table.keys() 
                         if self._states_similar(state, json.loads(k))]
        action_counts = defaultdict(int)
        for similar_state_key in similar_states:
            for action in self.q_table[similar_state_key]:
                action_counts[action] += 1
        min_count = min(action_counts.values()) if action_counts else 0
        least_explored = [action for action, count in action_counts.items() 
                         if count == min_count]
        if least_explored:
            return np.random.choice(least_explored)
        return np.random.choice(self.actions)
    
    def _states_similar(self, state1, state2, threshold=0.7):
        """Check if two states are similar"""
        common_keys = set(state1.keys()) & set(state2.keys())
        if not common_keys:
            return False
        matches = sum(1 for key in common_keys if state1[key] == state2[key])
        return matches / len(common_keys) >= threshold
    
    def _ensemble_action_selection(self, state, state_key):
        """Combine multiple selection methods for robust decisions"""
        methods = []
        q_values = self.q_table[state_key]
        if q_values:
            q_action = max(q_values.items(), key=lambda x: x[1])[0]
            methods.append(('q_learning', q_action, max(q_values.values())))
        success_rates = self.action_success_rates[state_key]
        if success_rates:
            success_action = max(success_rates.items(), 
                               key=lambda x: np.mean(x[1]) if x[1] else 0)[0]
            avg_success = np.mean(success_rates[success_action]) if success_rates[success_action] else 0
            methods.append(('success_rate', success_action, avg_success))
        pattern_action = self.pattern_analyzer.recommend_action(state)
        if pattern_action:
            confidence = self.pattern_analyzer.get_confidence(state, pattern_action)
            methods.append(('pattern', pattern_action, confidence))
        circadian_action = self.circadian_optimizer.recommend_action(state)
        if circadian_action:
            circadian_confidence = self.circadian_optimizer.get_confidence()
            methods.append(('circadian', circadian_action, circadian_confidence))
        if methods:
            action_scores = defaultdict(float)
            for method_name, action, confidence in methods:
                weight = confidence * self._get_method_weight(method_name)
                action_scores[action] += weight
            if action_scores:
                return max(action_scores.items(), key=lambda x: x[1])[0]
        return self._intelligent_rule_based_fallback(state)
    
    def _get_method_weight(self, method_name):
        """Get weight for ensemble method"""
        weights = {
            'q_learning': 1.0,
            'success_rate': 0.8,
            'pattern': 0.6,
            'circadian': 0.5
        }
        return weights.get(method_name, 0.5)
    
    def _intelligent_rule_based_fallback(self, state):
        """Intelligent rule-based fallback"""
        app_category = state.get('current_app_category', 'other')
        duration = state.get('session_duration_bin', 'short')
        energy = state.get('energy_level_estimate', 'medium')
        if app_category in ['entertainment', 'social'] and duration in ['long', 'very_long']:
            return 'task_prioritization'
        if energy == 'low':
            return 'energy_boost_suggestion'
        if app_category in ['development', 'productivity'] and duration == 'very_long':
            return 'micro_break_5min'
        return 'no_intervention'
    
    def _validate_and_refine_action(self, action, state, predicted_context):
        """Validate and refine the selected action"""
        if self._is_flow_state_context({'state': state}):
            if action not in ['no_intervention', 'micro_break_5min']:
                return 'no_intervention'
        if not self.user_profile.would_accept_action(action, state):
            similar_actions = self._get_similar_actions(action)
            for similar_action in similar_actions:
                if self.user_profile.would_accept_action(similar_action, state):
                    return similar_action
        return action
    
    def _get_similar_actions(self, action):
        """Get actions similar to the given action"""
        action_groups = {
            'gentle': ['no_intervention', 'gentle_refocus', 'mindfulness_prompt'],
            'break': ['micro_break_5min', 'energy_boost_suggestion'],
            'productivity': ['task_prioritization', 'deep_work_mode', 'productivity_gamification'],
            'blocking': ['adaptive_blocking', 'environment_optimization']
        }
        for group, actions in action_groups.items():
            if action in actions:
                return [a for a in actions if a != action]
        return []
    
    def _is_flow_state_context(self, context):
        """Check if user is in flow state"""
        state = context.get('state', {})
        return (state.get('focus_momentum') == 'high' and 
                state.get('cognitive_load_estimate') == 'high' and
                state.get('session_duration_bin') in ['medium', 'long'])
    
    def _is_high_stakes_context(self, context):
        """Check if this is a high-stakes work context"""
        return context.get('high_stakes_work', False)
    
    def learn_from_enhanced_feedback(self, state, action, feedback_data, context):
        """Multi-dimensional learning from feedback"""
        state_key = self.state_to_key(state)
        if state_key not in self.q_table:
            self.q_table[state_key] = {action: 0.0 for action in self.actions}
        reward = self._calculate_enhanced_reward(feedback_data, context, state, action)
        current_q = self.q_table[state_key][action]
        adaptive_lr = self._calculate_adaptive_learning_rate(state_key, action)
        new_q = current_q + adaptive_lr * (reward - current_q)
        self.q_table[state_key][action] = new_q
        self.action_success_rates[state_key][action].append(reward > 0)
        if len(self.action_success_rates[state_key][action]) > 20:
            self.action_success_rates[state_key][action].pop(0)
        self.implicit_learner.update_from_feedback(state, action, reward, context)
        self.pattern_analyzer.update_from_feedback(state, action, feedback_data)
        self.circadian_optimizer.update_from_feedback(state, action, reward, context)
        self.user_profile.update_preferences(state, action, feedback_data)
        feedback_entry = {
            'state_key': state_key,
            'state': state,
            'action': action,
            'reward': reward,
            'feedback': feedback_data,
            'context': context,
            'timestamp': datetime.now().isoformat(),
            'learning_rate_used': adaptive_lr,
            'q_value_change': new_q - current_q
        }
        self.feedback_history.append(feedback_entry)
        self._update_exploration_rate(reward)
        print(f"Enhanced Learning: {action} -> reward: {reward:.2f}, Q: {new_q:.3f} (lr: {adaptive_lr:.3f})")
    
    def _calculate_adaptive_learning_rate(self, state_key, action):
        """Calculate adaptive learning rate"""
        base_lr = self.learning_rate
        visit_count = self.state_visits.get(state_key, 0)
        if visit_count > 10:
            base_lr *= (10 / visit_count) ** 0.5
        if action not in self.q_table[state_key] or abs(self.q_table[state_key][action]) < 0.1:
            base_lr *= 1.5
        return np.clip(base_lr, 0.01, 0.5)
    
    def _update_exploration_rate(self, reward):
        """Update exploration rate based on reward"""
        if reward > 0:
            self.epsilon = max(self.epsilon_min, self.epsilon * self.epsilon_decay)
        else:
            self.epsilon = min(0.4, self.epsilon * 1.001)
    
    def _calculate_enhanced_reward(self, feedback_data, context, state, action):
        """Sophisticated reward calculation considering multiple factors"""
        base_reward = 0.0
        user_reliability = self.user_profile.get_feedback_reliability()
        if feedback_data.get('helpful', False):
            base_reward += 1.5 * user_reliability
        elif feedback_data.get('helpful') == False:
            base_reward -= 0.8 * user_reliability
        action_taken = feedback_data.get('user_action', 'none')
        action_rewards = {
            'acted_immediately': 2.0,
            'acted_later': 1.2,
            'dismissed_politely': -0.2,
            'dismissed_annoyed': -1.0,
            'ignored': -0.1,
            'customized': 1.8
        }
        base_reward += action_rewards.get(action_taken, 0.0)
        productivity_change = feedback_data.get('productivity_change', 0)
        base_reward += productivity_change * 1.5
        if self._is_high_stakes_context(context):
            base_reward *= 1.3
        if self._is_flow_state_context(context):
            if action in ['no_intervention', 'micro_break_5min']:
                base_reward += 0.5
            else:
                base_reward -= 0.3
        if self.circadian_optimizer.is_action_time_appropriate(action, context):
            base_reward += 0.3
        if feedback_data.get('long_term_helpful', False):
            base_reward += 0.7
        if self.user_profile.aligns_with_preferences(action, state):
            base_reward += 0.4
        return np.clip(base_reward, -3.0, 3.0)
    
    def get_personalized_intervention_message(self, action, state, context):
        """Generate highly personalized intervention messages"""
        communication_style = self.user_profile.get_communication_style()
        motivation_type = self.user_profile.get_motivation_type()
        base_messages = self._get_base_messages(action)
        if communication_style == 'direct':
            message_style = 'brief_direct'
        elif communication_style == 'encouraging':
            message_style = 'supportive'
        elif communication_style == 'analytical':
            message_style = 'data_driven'
        else:
            message_style = 'balanced'
        message = self._generate_contextual_message(
            action, state, context, message_style, motivation_type
        )
        if self.user_profile.likes_gamification():
            message = self._add_gamification_elements(message, action)
        if self.user_profile.responds_to_statistics():
            message = self._add_statistical_context(message, state)
        return message
    
    def _get_base_messages(self, action):
        """Get base messages for each action"""
        return {
            'no_intervention': None,
            'micro_break_5min': "Consider taking a 5-minute break to refresh your focus.",
            'gentle_refocus': "Gently redirect your attention to your main task.",
            'task_prioritization': "Review your current priorities and focus on what's most important.",
            'environment_optimization': "Optimize your workspace for better focus.",
            'energy_boost_suggestion': "Your energy seems low. Try a quick walk or some water.",
            'deep_work_mode': "Enter deep work mode for sustained concentration.",
            'collaborative_focus': "Consider collaborating with a colleague to boost focus.",
            'mindfulness_prompt': "Take a moment for mindful breathing.",
            'productivity_gamification': "Ready for a productivity challenge?",
            'adaptive_blocking': "Consider blocking distracting websites temporarily.",
            'cognitive_load_reduction': "Simplify your current task to reduce cognitive load.",
            'flow_state_induction': "Set up conditions to enter a flow state."
        }.get(action, "Focus on your current task.")
    
    def _generate_contextual_message(self, action, state, context, message_style, motivation_type):
        """Generate contextual message based on style and motivation"""
        base_message = self._get_base_messages(action)
        if not base_message:
            return None
        if message_style == 'brief_direct':
            return self._make_direct(base_message)
        elif message_style == 'supportive':
            return self._make_supportive(base_message, state)
        elif message_style == 'data_driven':
            return self._make_analytical(base_message, state)
        return base_message
    
    def _make_direct(self, message):
        """Make message more direct and brief"""
        direct_replacements = {
            "Consider taking": "Take",
            "Try a quick": "Get some",
            "Review your": "Check your",
            "Ready for": "Time for"
        }
        for old, new in direct_replacements.items():
            message = message.replace(old, new)
        return message
    
    def _make_supportive(self, message, state):
        """Add supportive elements to message"""
        energy = state.get('energy_level_estimate', 'medium')
        if energy == 'low':
            return f"You've been working hard. {message} You deserve a moment to recharge."
        else:
            return f"Great focus so far! {message} This will help maintain your momentum."
    
    def _make_analytical(self, message, state):
        """Add analytical context to message"""
        session_duration = state.get('session_duration_bin', 'medium')
        productivity = state.get('recent_productivity_score', 'medium')
        context_info = f"Based on your {session_duration} session and {productivity} productivity trend: "
        return context_info + message.lower()
    
    def _add_gamification_elements(self, message, action):
        """Add gamification elements to message"""
        if not message:
            return None
        gamification_prefixes = {
            'micro_break_5min': "Achievement Unlocked: Time for a Strategic Pause! ",
            'task_prioritization': "Mission Update: ",
            'deep_work_mode': "Boss Battle Mode: ",
            'productivity_gamification': "Challenge Accepted: "
        }
        prefix = gamification_prefixes.get(action, "Quest Update: ")
        return prefix + message
    
    def _add_statistical_context(self, message, state):
        """Add statistical context to message"""
        if not message:
            return None
        focus_momentum = state.get('focus_momentum', 'medium')
        stats_suffix = f" (Current focus momentum: {focus_momentum})"
        return message + stats_suffix
    
    def get_advanced_model_insights(self):
        """Comprehensive model insights and analytics"""
        insights = {
            'learning_metrics': self._get_learning_metrics(),
            'behavioral_patterns': self.pattern_analyzer.get_comprehensive_patterns(),
            'circadian_insights': self.circadian_optimizer.get_insights(),
            'personalization_profile': self.user_profile.get_profile_summary(),
            'prediction_accuracy': self.context_predictor.get_accuracy_metrics(),
            'adaptive_performance': self.adaptive_thresholds.get_performance_summary(),
            'state_space_coverage': self._analyze_state_space_coverage(),
            'action_effectiveness': self._analyze_action_effectiveness(),
            'user_engagement_trends': self._analyze_user_engagement()
        }
        return insights
    
    def _get_learning_metrics(self):
        """Get comprehensive learning metrics"""
        total_states = len(self.q_table)
        total_feedback = len(self.feedback_history)
        all_q_values = []
        for state_actions in self.q_table.values():
            all_q_values.extend(state_actions.values())
        q_stats = {
            'mean': np.mean(all_q_values) if all_q_values else 0,
            'std': np.std(all_q_values) if all_q_values else 0,
            'min': np.min(all_q_values) if all_q_values else 0,
            'max': np.max(all_q_values) if all_q_values else 0
        }
        recent_rewards = [f.get('reward', 0) for f in list(self.feedback_history)[-20:]]
        recent_performance = np.mean(recent_rewards) if recent_rewards else 0
        return {
            'total_states_explored': total_states,
            'total_feedback_received': total_feedback,
            'q_value_statistics': q_stats,
            'recent_performance': recent_performance,
            'exploration_rate': self.epsilon,
            'learning_rate': self.learning_rate
        }
    
    def _analyze_state_space_coverage(self):
        """Analyze how well we've covered the state space"""
        state_visits = list(self.state_visits.values())
        return {
            'total_unique_states': len(self.state_visits),
            'average_visits_per_state': np.mean(state_visits) if state_visits else 0,
            'most_visited_state_visits': max(state_visits) if state_visits else 0,
            'least_visited_state_visits': min(state_visits) if state_visits else 0
        }
    
    def _analyze_action_effectiveness(self):
        """Analyze effectiveness of different actions"""
        action_rewards = defaultdict(list)
        for feedback in self.feedback_history:
            action = feedback.get('action')
            reward = feedback.get('reward', 0)
            if action:
                action_rewards[action].append(reward)
        effectiveness = {}
        for action, rewards in action_rewards.items():
            effectiveness[action] = {
                'average_reward': np.mean(rewards),
                'success_rate': sum(1 for r in rewards if r > 0) / len(rewards),
                'total_uses': len(rewards)
            }
        return effectiveness
    
    def _analyze_user_engagement(self):
        """Analyze user engagement trends"""
        if len(self.feedback_history) < 5:
            return {'trend': 'insufficient_data'}
        recent_feedback = list(self.feedback_history)[-20:]
        engagement_scores = []
        for feedback in recent_feedback:
            response_time = feedback.get('context', {}).get('response_time_minutes', 10)
            user_action = feedback.get('feedback', {}).get('user_action', 'none')
            engagement = 0.5
            if user_action in ['acted_immediately', 'acted_later']:
                engagement += 0.4
            elif user_action == 'customized':
                engagement += 0.5
            elif user_action in ['dismissed_politely']:
                engagement -= 0.2
            elif user_action in ['dismissed_annoyed']:
                engagement -= 0.5
            if response_time < 2:
                engagement += 0.2
            elif response_time > 10:
                engagement -= 0.1
            engagement_scores.append(engagement)
        trend_slope = 0
        if len(engagement_scores) > 1:
            x = np.arange(len(engagement_scores))
            trend_slope = np.polyfit(x, engagement_scores, 1)[0]
        return {
            'average_engagement': np.mean(engagement_scores),
            'trend': 'increasing' if trend_slope > 0.01 else 'decreasing' if trend_slope < -0.01 else 'stable',
            'trend_strength': abs(trend_slope)
        }
    
    def save_enhanced_model(self):
        """Save comprehensive model with all subsystems"""
        try:
            model_dir = os.path.join(os.path.dirname(__file__), 'models')
            os.makedirs(model_dir, exist_ok=True)
            model_data = {
                'version': '2.0',
                'timestamp': datetime.now().isoformat(),
                'core_rl': {
                    'q_table': {k: dict(v) for k, v in self.q_table.items()},
                    'state_visits': dict(self.state_visits),
                    'action_success_rates': {
                        k: {action: list(rates) for action, rates in v.items()}
                        for k, v in self.action_success_rates.items()
                    },
                    'parameters': {
                        'learning_rate': self.learning_rate,
                        'epsilon': self.epsilon,
                        'discount_factor': self.discount_factor
                    }
                },
                'subsystems': {
                    'user_profile': self.user_profile.to_dict(),
                    'pattern_analyzer': self.pattern_analyzer.to_dict(),
                    'circadian_optimizer': self.circadian_optimizer.to_dict(),
                    'adaptive_thresholds': self.adaptive_thresholds.to_dict(),
                    'context_predictor': self.context_predictor.to_dict()
                },
                'feedback_history': list(self.feedback_history)[-500:],
                'model_stats': self.get_advanced_model_insights()
            }
            json_path = os.path.join(model_dir, 'enhanced_focus_rl_model.json')
            with open(json_path, 'w') as f:
                json.dump(model_data, f, indent=2, default=str)
            pkl_path = os.path.join(model_dir, 'enhanced_focus_rl_model.pkl')
            with open(pkl_path, 'wb') as f:
                pickle.dump(model_data, f)
            backup_path = os.path.join(model_dir, f'backup_{int(datetime.now().timestamp())}.pkl')
            with open(backup_path, 'wb') as f:
                pickle.dump(model_data, f)
            print(f"Enhanced model saved: {len(self.q_table)} states, "
                  f"{len(self.feedback_history)} feedback entries")
        except Exception as e:
            print(f"Error saving enhanced model: {e}")
    
    def load_model(self):
        """Load enhanced model with backward compatibility"""
        try:
            model_dir = os.path.join(os.path.dirname(__file__), 'models')
            pkl_path = os.path.join(model_dir, 'enhanced_focus_rl_model.pkl')
            json_path = os.path.join(model_dir, 'enhanced_focus_rl_model.json')

            # Try loading from pickle
            if os.path.exists(pkl_path):
                if os.path.getsize(pkl_path) == 0:
                    print(f"Error: {pkl_path} is empty")
                else:
                    with open(pkl_path, 'rb') as f:
                        model_data = pickle.load(f)
                    self._load_from_json_data(model_data)
                    print(f"Enhanced model loaded from pickle: {len(self.q_table)} states")
                    return

            # Try loading from JSON
            if os.path.exists(json_path):
                if os.path.getsize(json_path) == 0:
                    print(f"Error: {json_path} is empty")
                else:
                    with open(json_path, 'r') as f:
                        model_data = json.load(f)
                    self._load_from_json_data(model_data)
                    print(f"Enhanced model loaded from JSON: {len(self.q_table)} states")
                    return

            print("No valid model files found. Starting with fresh enhanced model")
        except Exception as e:
            print(f"Could not load enhanced model: {str(e)}")
            print("Starting with fresh enhanced model")
    
    def _load_from_json_data(self, model_data):
        """Load from JSON or pickle data"""
        core_rl = model_data.get('core_rl', {})
        q_table_data = core_rl.get('q_table', {})
        self.q_table = {k: v for k, v in q_table_data.items()}
        self.state_visits = defaultdict(int, core_rl.get('state_visits', {}))
        self.feedback_history = deque(model_data.get('feedback_history', []), maxlen=1500)
        params = core_rl.get('parameters', {})
        self.epsilon = params.get('epsilon', self.epsilon)
    
    def reset(self):
        """Reset the agent to initial state"""
        self.q_table = {}
        self.state_visits.clear()
        self.action_success_rates.clear()
        self.feedback_history.clear()
        self.user_sessions.clear()
        self.intervention_history.clear()
        self.epsilon = 0.2
        self.implicit_learner = ImplicitLearningSystem()
        self.pattern_analyzer = UserPatternAnalyzer()
        self.context_predictor = ContextPredictor()
        self.user_profile = UserProfile()
        self.adaptive_thresholds = AdaptiveThresholds()
        self.temporal_patterns = TemporalPatterns()
        self.circadian_optimizer = CircadianOptimizer()
        self.behavioral_metrics = BehavioralMetrics()
        print("🧠 EnhancedFocusRLAgent has been reset.")
    
    def demonstrate_learning(self):
        """Demonstrate learning progress with detailed insights"""
        print("\n🧠 ENHANCED LEARNING DEMONSTRATION")
        print("=" * 50)
        stats = self.get_advanced_model_insights()
        print(f"Model Status: {stats['learning_metrics']['learning_rate']:.3f} learning rate")
        print(f"Total States Explored: {stats['learning_metrics']['total_states_explored']}")
        print(f"Total Feedback Received: {stats['learning_metrics']['total_feedback_received']}")
        print(f"Current Exploration Rate: {stats['learning_metrics']['exploration_rate']:.3f}")
        if self.q_table:
            print("\nTop 5 Learned State-Action Pairs:")
            all_state_actions = []
            for state_key, actions in self.q_table.items():
                for action, q_val in actions.items():
                    all_state_actions.append((state_key[:50] + "...", action, q_val))
            top_pairs = sorted(all_state_actions, key=lambda x: x[2], reverse=True)[:5]
            for i, (state, action, q_val) in enumerate(top_pairs, 1):
                print(f"  {i}. {action} → Q-value: {q_val:.3f}")
                print(f"     State: {state}")
        print("\nBehavioral Patterns:")
        for pattern in stats['behavioral_patterns'].items():
            print(f"  {pattern[0]}: {pattern[1]}")
        return stats

class ImplicitLearningSystem:
    """Learn from user behavior patterns without explicit feedback"""
    def __init__(self):
        self.behavior_patterns = defaultdict(list)
        self.productivity_indicators = {}
        self.attention_metrics = {}
    
    def learn_from_selection(self, state, action, predicted_context):
        """Learn from action selection patterns"""
        pattern_key = f"{state.get('current_app_category')}_{state.get('time_of_day_bin')}"
        self.behavior_patterns[pattern_key].append({
            'action': action,
            'timestamp': datetime.now().isoformat(),
            'context': predicted_context
        })
    
    def update_from_feedback(self, state, action, reward, context):
        """Update implicit models from feedback"""
        app_category = state.get('current_app_category', 'unknown')
        if app_category not in self.productivity_indicators:
            self.productivity_indicators[app_category] = []
        self.productivity_indicators[app_category].append({
            'action': action,
            'reward': reward,
            'timestamp': datetime.now().isoformat()
        })

class UserPatternAnalyzer:
    """Analyze and predict user behavioral patterns"""
    def __init__(self):
        self.daily_patterns = defaultdict(list)
        self.weekly_patterns = defaultdict(list)
        self.app_usage_patterns = defaultdict(list)
        self.productivity_cycles = {}
    
    def update_patterns(self, sessions):
        """Update pattern analysis from sessions"""
        for session in sessions[-50:]:
            timestamp = session.get('timestamp', datetime.now().isoformat())
            dt = datetime.fromisoformat(timestamp.replace('Z', '+00:00'))
            hour_key = f"hour_{dt.hour}"
            day_key = dt.strftime('%A').lower()
            self.daily_patterns[hour_key].append(session)
            self.weekly_patterns[day_key].append(session)
            app = session.get('app_name', 'unknown')
            self.app_usage_patterns[app].append(session)
    
    def recommend_action(self, state):
        """Recommend action based on patterns"""
        time_bin = state.get('time_of_day_bin', 'morning')
        hour_patterns = self.daily_patterns.get(f"hour_{datetime.now().hour}", [])
        if not hour_patterns:
            return None
        successful_actions = []
        for pattern in hour_patterns[-10:]:
            if pattern.get('success', False):
                successful_actions.append(pattern.get('action', 'no_intervention'))
        if successful_actions:
            return max(set(successful_actions), key=successful_actions.count)
        return None
    
    def get_confidence(self, state, action):
        """Get confidence in pattern-based recommendation"""
        time_bin = state.get('time_of_day_bin', 'morning')
        relevant_patterns = self.daily_patterns.get(f"hour_{datetime.now().hour}", [])
        if not relevant_patterns:
            return 0.0
        action_count = sum(1 for p in relevant_patterns if p.get('action') == action)
        return min(action_count / len(relevant_patterns), 1.0)
    
    def update_from_feedback(self, state, action, feedback_data):
        """Update patterns from feedback"""
        success = feedback_data.get('helpful', False) or feedback_data.get('user_action') in ['acted_immediately', 'acted_later']
        pattern_entry = {
            'state': state,
            'action': action,
            'success': success,
            'timestamp': datetime.now().isoformat()
        }
        hour_key = f"hour_{datetime.now().hour}"
        self.daily_patterns[hour_key].append(pattern_entry)
    
    def get_comprehensive_patterns(self):
        """Get comprehensive pattern analysis"""
        return {
            'daily_patterns_count': len(self.daily_patterns),
            'weekly_patterns_count': len(self.weekly_patterns),
            'most_active_hours': self._get_most_active_hours(),
            'productivity_trends': self._get_productivity_trends()
        }
    
    def _get_most_active_hours(self):
        """Get most active hours"""
        hour_counts = {hour: len(patterns) for hour, patterns in self.daily_patterns.items()}
        return sorted(hour_counts.items(), key=lambda x: x[1], reverse=True)[:3]
    
    def _get_productivity_trends(self):
        """Get productivity trends"""
        return {'trend': 'stable'}
    
    def to_dict(self):
        """Convert to dictionary for serialization"""
        return {
            'daily_patterns_keys': list(self.daily_patterns.keys()),
            'weekly_patterns_keys': list(self.weekly_patterns.keys()),
            'pattern_count': len(self.daily_patterns) + len(self.weekly_patterns)
        }

class ContextPredictor:
    """Predict future contexts and user needs"""
    def __init__(self):
        self.context_history = deque(maxlen=1000)
        self.prediction_model = None
        self.accuracy_history = deque(maxlen=100)
    
    def predict_next_context(self, current_state):
        """Predict what will happen next"""
        time_bin = current_state.get('time_of_day_bin', 'morning')
        time_based_predictions = {
            'morning': {'likely_apps': ['productivity', 'communication'], 'energy': 'high'},
            'afternoon': {'likely_apps': ['development', 'research'], 'energy': 'medium'},
            'evening': {'likely_apps': ['entertainment', 'social'], 'energy': 'low'}
        }
        return time_based_predictions.get(time_bin, {'likely_apps': ['unknown'], 'energy': 'medium'})
    
    def train_on_recent_data(self, recent_sessions):
        """Train prediction model on recent data"""
        self.context_history.extend(recent_sessions[-50:])
    
    def get_accuracy_metrics(self):
        """Get prediction accuracy metrics"""
        if not self.accuracy_history:
            return {'accuracy': 0.5, 'sample_size': 0}
        return {
            'accuracy': np.mean(self.accuracy_history),
            'sample_size': len(self.accuracy_history)
        }
    
    def to_dict(self):
        """Convert to dictionary"""
        return {
            'context_history_size': len(self.context_history),
            'accuracy_history_size': len(self.accuracy_history)
        }

class BehavioralMetrics:
    """Track detailed behavioral metrics"""
    def __init__(self):
        self.metrics = {
            'focus_streaks': [],
            'distraction_events': [],
            'productivity_scores': [],
            'intervention_responses': []
        }
    
    def update_metrics(self, session, feedback):
        """Update behavioral metrics"""
        self.metrics['productivity_scores'].append(session.get('productivity_score', 0.5))
        if feedback.get('user_action') in ['dismissed_politely', 'dismissed_annoyed']:
            self.metrics['distraction_events'].append({
                'timestamp': datetime.now().isoformat(),
                'app_category': session.get('app_category', 'unknown')
            })
        if session.get('focus_duration', 0) > 30:
            self.metrics['focus_streaks'].append({
                'duration': session.get('focus_duration', 0),
                'timestamp': datetime.now().isoformat()
            })
    
    def get_metrics_summary(self):
        """Get summary of behavioral metrics"""
        return {
            'focus_streaks_count': len(self.metrics['focus_streaks']),
            'distraction_events_count': len(self.metrics['distraction_events']),
            'average_productivity': np.mean(self.metrics['productivity_scores']) if self.metrics['productivity_scores'] else 0
        }
    
    def to_dict(self):
        """Convert to dictionary"""
        return self.get_metrics_summary()

class UserProfile:
    """Maintain detailed user preferences and characteristics"""
    def __init__(self):
        self.preferences = {
            'communication_style': 'balanced',
            'motivation_type': 'achievement',
            'gamification_preference': True,
            'statistics_interest': True
        }
        self.feedback_reliability = 0.8
        self.response_patterns = defaultdict(list)
    
    def get_communication_style(self):
        return self.preferences.get('communication_style', 'balanced')
    
    def get_motivation_type(self):
        return self.preferences.get('motivation_type', 'achievement')
    
    def get_feedback_reliability(self):
        return self.feedback_reliability
    
    def update_preferences(self, state, action, feedback):
        """Update user preferences based on feedback"""
        user_action = feedback.get('user_action', 'none')
        helpful = feedback.get('helpful', False)
        pattern_key = f"{action}_{state.get('current_app_category', 'unknown')}"
        self.response_patterns[pattern_key].append({
            'helpful': helpful,
            'user_action': user_action,
            'timestamp': datetime.now().isoformat()
        })
        if user_action == 'dismissed_annoyed':
            current_style = self.preferences.get('communication_style', 'balanced')
            if current_style == 'encouraging':
                self.preferences['communication_style'] = 'balanced'
            elif current_style == 'balanced':
                self.preferences['communication_style'] = 'direct'
    
    def likes_gamification(self):
        return self.preferences.get('gamification_preference', True)
    
    def responds_to_statistics(self):
        return self.preferences.get('statistics_interest', True)
    
    def aligns_with_preferences(self, action, state):
        """Check if action aligns with user preferences"""
        communication_style = self.get_communication_style()
        if communication_style == 'direct':
            return action in ['no_intervention', 'gentle_refocus']
        if communication_style == 'encouraging':
            return action not in ['adaptive_blocking']
        return True
    
    def would_accept_action(self, action, state):
        """Predict if user would accept this action"""
        pattern_key = f"{action}_{state.get('current_app_category', 'unknown')}"
        pattern_history = self.response_patterns.get(pattern_key, [])
        if not pattern_history:
            return True
        recent_patterns = pattern_history[-5:]
        positive_responses = sum(1 for p in recent_patterns 
                               if p.get('helpful', False) or 
                               p.get('user_action') in ['acted_immediately', 'acted_later'])
        acceptance_rate = positive_responses / len(recent_patterns) if recent_patterns else 0
        return acceptance_rate > 0.3
    
    def get_profile_summary(self):
        """Get profile summary"""
        return {
            'preferences': self.preferences,
            'feedback_reliability': self.feedback_reliability,
            'response_patterns_count': len(self.response_patterns)
        }
    
    def to_dict(self):
        """Convert to dictionary"""
        return {
            'preferences': self.preferences,
            'feedback_reliability': self.feedback_reliability,
            'response_patterns_keys': list(self.response_patterns.keys())
        }

class AdaptiveThresholds:
    """Dynamically adjust intervention thresholds"""
    def __init__(self):
        self.thresholds = {
            'distraction_risk': 0.7,
            'productivity_concern': 0.3,
            'energy_low': 0.4,
            'focus_break_needed': 0.8
        }
        self.performance_history = deque(maxlen=100)
    
    def adjust_based_on_performance(self, feedback_history):
        """Adjust thresholds based on performance"""
        if len(feedback_history) < 10:
            return
        recent_feedback = list(feedback_history)[-20:]
        avg_reward = np.mean([f.get('reward', 0) for f in recent_feedback])
        if avg_reward < -0.2:
            for key in self.thresholds:
                self.thresholds[key] = min(self.thresholds[key] + 0.05, 0.9)
        elif avg_reward > 0.5:
            for key in self.thresholds:
                self.thresholds[key] = max(self.thresholds[key] - 0.05, 0.1)
        self.performance_history.append(avg_reward)
    
    def get_performance_summary(self):
        """Get performance summary"""
        if not self.performance_history:
            return {'trend': 'no_data'}
        recent_performance = list(self.performance_history)[-10:]
        return {
            'average_performance': np.mean(recent_performance),
            'performance_trend': 'improving' if len(recent_performance) > 1 and 
                               recent_performance[-1] > recent_performance[0] else 'stable',
            'current_thresholds': self.thresholds.copy()
        }
    
    def to_dict(self):
        """Convert to dictionary"""
        return {
            'thresholds': self.thresholds.copy(),
            'performance_history_size': len(self.performance_history)
        }

class TemporalPatterns:
    """Handle time-based pattern analysis"""
    def __init__(self):
        self.hourly_patterns = defaultdict(list)
        self.daily_patterns = defaultdict(list)
        self.weekly_patterns = defaultdict(list)
    
    def update_from_session(self, session):
        """Update temporal patterns from session data"""
        timestamp = session.get('timestamp', datetime.now().isoformat())
        dt = datetime.fromisoformat(timestamp.replace('Z', '+00:00'))
        hour_key = f"hour_{dt.hour}"
        day_key = dt.strftime('%A').lower()
        week_phase = 'weekday' if dt.weekday() < 5 else 'weekend'
        self.hourly_patterns[hour_key].append(session)
        self.daily_patterns[day_key].append(session)
        self.weekly_patterns[week_phase].append(session)
    
    def get_temporal_insights(self):
        """Get insights from temporal patterns"""
        return {
            'hourly_pattern_count': {k: len(v) for k, v in self.hourly_patterns.items()},
            'daily_pattern_count': {k: len(v) for k, v in self.daily_patterns.items()},
            'weekly_pattern_count': {k: len(v) for k, v in self.weekly_patterns.items()}
        }
    
    def to_dict(self):
        """Convert to dictionary for serialization"""
        return {
            'hourly_patterns_keys': list(self.hourly_patterns.keys()),
            'daily_patterns_keys': list(self.daily_patterns.keys()),
            'weekly_patterns_keys': list(self.weekly_patterns.keys())
        }

class CircadianOptimizer:
    """Optimize interventions based on circadian rhythms"""
    def __init__(self):
        self.circadian_data = {}
        self.energy_patterns = defaultdict(list)
        self.optimal_times = {}
    
    def estimate_energy_level(self, timestamp):
        """Estimate energy level based on time"""
        hour = timestamp.hour
        if 6 <= hour <= 10:
            return 'high'
        elif 10 <= hour <= 14:
            return 'medium_high'
        elif 14 <= hour <= 16:
            return 'medium'
        elif 16 <= hour <= 19:
            return 'medium_high'
        elif 19 <= hour <= 22:
            return 'medium'
        else:
            return 'low'
    
    def get_circadian_phase(self, timestamp):
        """Get current circadian phase"""
        hour = timestamp.hour
        if 6 <= hour < 12:
            return 'morning'
        elif 12 <= hour < 18:
            return 'afternoon'
        elif 18 <= hour < 24:
            return 'evening'
        else:
            return 'night'
    
    def recommend_action(self, state):
        """Recommend action based on circadian rhythms"""
        current_phase = self.get_circadian_phase(datetime.now())
        energy_level = state.get('energy_level_estimate', 'medium')
        if current_phase == 'morning' and energy_level in ['high', 'medium_high']:
            return 'deep_work_mode'
        if current_phase == 'afternoon' and energy_level == 'medium':
            return 'energy_boost_suggestion'
        if current_phase == 'evening':
            return 'gentle_refocus'
        return None
    
    def get_confidence(self):
        """Get confidence in circadian recommendations"""
        return 0.6
    
    def update_from_feedback(self, state, action, reward, context):
        """Update circadian optimization from feedback"""
        hour = datetime.now().hour
        phase = self.get_circadian_phase(datetime.now())
        if phase not in self.energy_patterns:
            self.energy_patterns[phase] = []
        self.energy_patterns[phase].append({
            'hour': hour,
            'action': action,
            'reward': reward,
            'energy_estimate': state.get('energy_level_estimate', 'medium')
        })
    
    def update_preferences(self, feedback_history):
        """Update circadian preferences based on feedback"""
        time_performance = defaultdict(list)
        for feedback in list(feedback_history)[-50:]:
            timestamp_str = feedback.get('timestamp', '')
            if timestamp_str:
                try:
                    dt = datetime.fromisoformat(timestamp_str.replace('Z', '+00:00'))
                    phase = self.get_circadian_phase(dt)
                    reward = feedback.get('reward', 0)
                    time_performance[phase].append(reward)
                except:
                    continue
        for phase, rewards in time_performance.items():
            if rewards:
                self.optimal_times[phase] = np.mean(rewards)
    
    def is_action_time_appropriate(self, action, context):
        """Check if action is appropriate for current time"""
        current_phase = self.get_circadian_phase(datetime.now())
        time_appropriate_actions = {
            'morning': ['deep_work_mode', 'task_prioritization', 'productivity_gamification'],
            'afternoon': ['micro_break_5min', 'energy_boost_suggestion'],
            'evening': ['gentle_refocus', 'mindfulness_prompt'],
            'night': ['no_intervention']
        }
        appropriate_actions = time_appropriate_actions.get(current_phase, [])
        return action in appropriate_actions or action == 'no_intervention'
    
    def get_insights(self):
        """Get circadian insights"""
        return {
            'energy_patterns_phases': list(self.energy_patterns.keys()),
            'optimal_times': self.optimal_times.copy(),
            'total_data_points': sum(len(patterns) for patterns in self.energy_patterns.values())
        }
    
    def to_dict(self):
        """Convert to dictionary"""
        return {
            'energy_patterns_keys': list(self.energy_patterns.keys()),
            'optimal_times': self.optimal_times.copy(),
            'total_data_points': sum(len(patterns) for patterns in self.energy_patterns.values())
        }