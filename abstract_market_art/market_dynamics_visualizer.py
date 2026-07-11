import matplotlib.pyplot as plt
import numpy as np
import json
from datetime import datetime
import matplotlib.patches as patches

# Load MT4 data
positions_data = {
  "positions": [
    {
      "Ticket": 28459026,
      "Symbol": "EURUSD",
      "Type": 0,
      "Volume": 0.01,
      "OpenPrice": 1.14153,
      "OpenTime": "2026.07.06 14:44:42",
      "CurrentPrice": 1.14132,
      "StopLoss": 1.13939,
      "TakeProfit": 1.14769,
      "Profit": -0.21,
      "Comment": "HouseVictoria"
    },
    {
      "Ticket": 28459028,
      "Symbol": "EURUSD",
      "Type": 0,
      "Volume": 0.01,
      "OpenPrice": 1.14153,
      "OpenTime": "2026.07.06 14:44:42",
      "CurrentPrice": 1.14132,
      "StopLoss": 1.13939,
      "TakeProfit": 1.14769,
      "Profit": -0.21,
      "Comment": "HouseVictoria"
    },
    {
      "Ticket": 28459030,
      "Symbol": "EURUSD",
      "Type": 0,
      "Volume": 0.01,
      "OpenPrice": 1.14153,
      "OpenTime": "2026.07.06 14:44:42",
      "CurrentPrice": 1.14132,
      "StopLoss": 1.13939,
      "TakeProfit": 1.14769,
      "Profit": -0.21,
      "Comment": "HouseVictoria"
    },
    {
      "Ticket": 28459032,
      "Symbol": "EURUSD",
      "Type": 0,
      "Volume": 0.01,
      "OpenPrice": 1.14153,
      "OpenTime": "2026.07.06 14:44:42",
      "CurrentPrice": 1.14132,
      "StopLoss": 1.13939,
      "TakeProfit": 1.14769,
      "Profit": -0.21,
      "Comment": "HouseVictoria"
    },
    {
      "Ticket": 28459034,
      "Symbol": "EURUSD",
      "Type": 0,
      "Volume": 0.01,
      "OpenPrice": 1.14153,
      "OpenTime": "2026.07.06 14:44:42",
      "CurrentPrice": 1.14132,
      "StopLoss": 1.13939,
      "TakeProfit": 1.14769,
      "Profit": -0.21,
      "Comment": "HouseVictoria"
    },
    {
      "Ticket": 28459036,
      "Symbol": "EURUSD",
      "Type": 0,
      "Volume": 0.01,
      "OpenPrice": 1.14153,
      "OpenTime": "2026.07.06 14:44:44",
      "CurrentPrice": 1.14132,
      "StopLoss": 1.13939,
      "TakeProfit": 1.14769,
      "Profit": -0.21,
      "Comment": "HouseVictoria"
    },
    {
      "Ticket": 28459038,
      "Symbol": "EURUSD",
      "Type": 0,
      "Volume": 0.01,
      "OpenPrice": 1.14153,
      "OpenTime": "2026.07.06 14:44:44",
      "CurrentPrice": 1.14132,
      "StopLoss": 1.13939,
      "TakeProfit": 1.14769,
      "Profit": -0.21,
      "Comment": "HouseVictoria"
    },
    {
      "Ticket": 28459040,
      "Symbol": "EURUSD",
      "Type": 0,
      "Volume": 0.01,
      "OpenPrice": 1.14153,
      "OpenTime": "2026.07.06 14:44:45",
      "CurrentPrice": 1.14132,
      "StopLoss": 1.13939,
      "TakeProfit": 1.14769,
      "Profit": -0.21,
      "Comment": "HouseVictoria"
    },
    {
      "Ticket": 28459042,
      "Symbol": "EURUSD",
      "Type": 0,
      "Volume": 0.01,
      "OpenPrice": 1.14153,
      "OpenTime": "2026.07.06 14:44:45",
      "CurrentPrice": 1.14132,
      "StopLoss": 1.13939,
      "TakeProfit": 1.14769,
      "Profit": -0.21,
      "Comment": "HouseVictoria"
    },
    {
      "Ticket": 28459044,
      "Symbol": "EURUSD",
      "Type": 0,
      "Volume": 0.01,
      "OpenPrice": 1.14153,
      "OpenTime": "2026.07.06 14:44:45",
      "CurrentPrice": 1.14132,
      "StopLoss": 1.13939,
      "TakeProfit": 1.14769,
      "Profit": -0.21,
      "Comment": "HouseVictoria"
    },
    {
      "Ticket": 28459046,
      "Symbol": "EURUSD",
      "Type": 0,
      "Volume": 0.01,
      "OpenPrice": 1.14153,
      "OpenTime": "2026.07.06 14:44:45",
      "CurrentPrice": 1.14132,
      "StopLoss": 1.13939,
      "TakeProfit": 1.14769,
      "Profit": -0.21,
      "Comment": "HouseVictoria"
    }
  ]
}

market_data = {
  "bid": 1.14132,
  "ask": 1.14146,
  "spread": 0.00014
}

watch_symbols = [
  "EURUSD", "GBPUSD", "USDJPY", "AUDUSD", "USDCAD", 
  "USDCHF", "NZDUSD", "EURGBP", "EURJPY", "GBPJPY", 
  "XAUUSD", "XAGUSD", "US30", "US500", "NAS100"
]

# Create figure and axis
fig, ax = plt.subplots(figsize=(16, 12))
fig.patch.set_facecolor('black')
ax.set_facecolor('black')

# Set title with market data
plt.suptitle('ABSTRACT MARKET DYNAMICS & SENSORY FEEDBACK LOOPS', 
             color='white', fontsize=24, y=0.95, fontweight='bold')

# Add market info text
info_text = f"EURUSD Bid: {market_data['bid']} | Ask: {market_data['ask']} | Spread: {market_data['spread']:.5f}"
plt.figtext(0.5, 0.90, info_text, ha='center', color='cyan', fontsize=14)

# Create visual representation of positions
positions = positions_data['positions']
num_positions = len(positions)

# Create concentric circles representing feedback loops
angles = np.linspace(0, 2*np.pi, num_positions, endpoint=False)
inner_radius = 2
outer_radius = 5

# Draw concentric circles
circle1 = plt.Circle((0, 0), 1.5, color='blue', alpha=0.1, fill=False)
circle2 = plt.Circle((0, 0), 3, color='green', alpha=0.1, fill=False)
circle3 = plt.Circle((0, 0), 4.5, color='red', alpha=0.1, fill=False)
ax.add_patch(circle1)
ax.add_patch(circle2)
ax.add_patch(circle3)

# Draw position markers with profit/loss feedback
for i, pos in enumerate(positions):
    angle = angles[i]
    radius = inner_radius + (i % 3) * 1.5  # Distribute across circles
    
    # Position marker
    x = radius * np.cos(angle)
    y = radius * np.sin(angle)
    
    # Color based on profit/loss
    profit = pos['Profit']
    if profit < 0:
        color = 'red'
        size = 100 * abs(profit)  # Size based on loss magnitude
    else:
        color = 'green'
        size = 100
    
    # Draw marker
    ax.scatter(x, y, c=color, s=size, alpha=0.7, edgecolors='white')
    
    # Add ticket number
    ax.text(x, y, str(pos['Ticket'] % 1000), 
            color='white', fontsize=8, ha='center', va='center')

# Draw connections between positions (feedback loops)
for i in range(num_positions):
    for j in range(i+1, num_positions):
        if i != j:
            angle1 = angles[i]
            angle2 = angles[j]
            radius1 = inner_radius + (i % 3) * 1.5
            radius2 = inner_radius + (j % 3) * 1.5
            
            x1 = radius1 * np.cos(angle1)
            y1 = radius1 * np.sin(angle1)
            x2 = radius2 * np.cos(angle2)
            y2 = radius2 * np.sin(angle2)
            
            # Draw connection lines with transparency
            ax.plot([x1, x2], [y1, y2], 'cyan', alpha=0.1, linewidth=0.5)

# Draw price movement visualization
price_range = [1.139, 1.148]  # Based on SL and TP values
current_price = market_data['bid']
open_price = positions[0]['OpenPrice']

# Draw price line
price_line_x = np.linspace(-6, 6, 100)
price_line_y = np.sin(price_line_x * 2) * 0.2  # Oscillating line for market dynamics

# Offset based on current price position
price_offset = (current_price - price_range[0]) / (price_range[1] - price_range[0]) * 4 - 2
price_line_y += price_offset

ax.plot(price_line_x, price_line_y, color='yellow', linewidth=2, alpha=0.7)
ax.scatter([0], [price_offset], color='orange', s=150, marker='o', edgecolors='white')

# Add market symbols as background elements
symbol_angles = np.linspace(0, 2*np.pi, len(watch_symbols), endpoint=False)
symbol_radius = 7
for i, symbol in enumerate(watch_symbols):
    angle = symbol_angles[i]
    x = symbol_radius * np.cos(angle)
    y = symbol_radius * np.sin(angle)
    ax.text(x, y, symbol, color='gray', fontsize=10, ha='center', va='center', alpha=0.5)

# Draw sensory feedback elements
# Draw random noise pattern to represent market volatility
np.random.seed(42)
noise_x = np.random.uniform(-8, 8, 50)
noise_y = np.random.uniform(-6, 6, 50)
noise_colors = np.random.rand(50)
ax.scatter(noise_x, noise_y, c=noise_colors, cmap='plasma', s=20, alpha=0.3)

# Draw technical indicators representation
indicator_x = np.linspace(-5, 5, 20)
for i in range(3):
    indicator_y = np.sin(indicator_x * (i+1)) * 0.5 + (i - 1)
    ax.plot(indicator_x, indicator_y, color=['red', 'blue', 'green'][i], 
            linewidth=1, alpha=0.5, linestyle=['-', '--', '-.'][i])

# Set limits and remove axes
ax.set_xlim(-8, 8)
ax.set_ylim(-6, 6)
ax.set_aspect('equal')
plt.axis('off')

# Add legend
legend_elements = [
    plt.Line2D([0], [0], marker='o', color='w', markerfacecolor='red', markersize=10, label='Loss Position'),
    plt.Line2D([0], [0], marker='o', color='w', markerfacecolor='green', markersize=10, label='Profit Position'),
    plt.Line2D([0], [0], color='cyan', linewidth=1, label='Feedback Connections'),
    plt.Line2D([0], [0], color='yellow', linewidth=2, label='Price Movement')
]
ax.legend(handles=legend_elements, loc='upper right', facecolor='black', edgecolor='white')

# Add timestamp
timestamp = datetime.now().strftime("%Y-%m-%d %H:%M:%S")
plt.figtext(0.02, 0.02, f"Generated: {timestamp}", color='white', fontsize=10)

# Save the visualization
plt.tight_layout()
plt.savefig('market_dynamics_abstract_art.png', 
            facecolor='black', edgecolor='none', 
            bbox_inches='tight', dpi=300)
plt.show()

print("Abstract market dynamics visualization created successfully!")