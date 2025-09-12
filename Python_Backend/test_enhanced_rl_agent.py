# test_enhanced_rl_agent.py
from enhanced_rl_agent import EnhancedFocusRLAgent
from datetime import datetime
import numpy as np

if __name__ == "__main__":
    print("🚀 Testing Enhanced RL Agent")
    
    agent = EnhancedFocusRLAgent()
    
    test_activities = [
        {'app_name': 'YouTube', 'window_title': 'Funny Cats', 'duration_minutes': 25, 'timestamp': datetime.now().isoformat()},
        {'app_name': 'VSCode', 'window_title': 'main.py', 'duration_minutes': 45, 'timestamp': datetime.now().isoformat()},
        {'app_name': 'Slack', 'window_title': 'Team Meeting', 'duration_minutes': 15, 'timestamp': datetime.now().isoformat()},
        {'app_name': 'Word', 'window_title': 'Report.docx', 'duration_minutes': 30, 'timestamp': datetime.now().isoformat()},
        {'app_name': 'Chrome', 'window_title': 'StackOverflow', 'duration_minutes': 20, 'timestamp': datetime.now().isoformat()}
    ]
    
    for activity in test_activities:
        context = {
            'recent_productivity_score': np.random.uniform(0.2, 0.8),
            'app_switches_last_hour': np.random.randint(1, 10),
            'social_context': 'solo',
            'high_stakes_work': np.random.choice([True, False])
        }
        agent.user_sessions.append(activity)  # Add to user_sessions
        state = agent.get_enhanced_state_vector(activity, context)
        action = agent.select_intelligent_action(state)
        message = agent.get_personalized_intervention_message(action, state, context)
        print(f"App: {activity['app_name']} ({activity['window_title']}) → Action: {action}")
        print(f"Message: {message}")
        if np.random.random() < 0.3:
            feedback = {
                'helpful': np.random.choice([True, False]),
                'user_action': np.random.choice(['acted_immediately', 'acted_later', 'dismissed_politely', 'dismissed_annoyed', 'ignored']),
                'productivity_change': np.random.uniform(-0.2, 0.3),
                'long_term_helpful': np.random.choice([True, False])
            }
            agent.learn_from_enhanced_feedback(state, action, feedback, context)
    
    agent.demonstrate_learning()
    agent.save_enhanced_model()
    print("\n✅ Enhanced model test complete!")