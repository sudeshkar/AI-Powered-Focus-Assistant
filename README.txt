# 🤖 AI-Powered Focus Assistant

## 📖 Project Description
An intelligent desktop application that uses reinforcement learning to help users manage digital distractions and improve productivity.

## 🛠️ Tech Stack
- **Frontend**: WPF (.NET 6)
- **Backend**: Python Flask
- **ML/AI**: Stable-Baselines3 (Reinforcement Learning)
- **Data**: JSON logging, Local storage

## 📁 Project Structure

## 🔒 Privacy & Security

This application:
- Stores all data locally (no cloud sync by default)
- Excludes sensitive files from git tracking
- Does not collect personal information
- User logs are stored in `/Data/` (gitignored)

### Important Files Not Tracked:
- User activity logs (`*.json`, `*.csv`)
- Trained ML models (`*.pkl`, `*.h5`)
- Configuration with sensitive data (`.env`)
- Build artifacts and temporary files