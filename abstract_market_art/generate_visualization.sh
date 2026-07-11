#!/bin/bash

echo "Abstract Market Dynamics Visualization Generator"
echo "=============================================="
echo
echo "Generating abstract visual art from MT4 data..."
echo

# Check if Python is installed
if ! command -v python3 &> /dev/null
then
    echo "Error: Python 3 is not installed"
    echo "Please install Python 3.7 or later and try again"
    exit 1
fi

# Check if required Python packages are installed
echo "Checking for required Python packages..."

if ! python3 -c "import matplotlib" &> /dev/null
then
    echo "Installing matplotlib..."
    pip3 install matplotlib
fi

if ! python3 -c "import numpy" &> /dev/null
then
    echo "Installing numpy..."
    pip3 install numpy
fi

# Run the visualization script
echo
echo "Running visualization script..."
python3 market_dynamics_visualizer.py

if [ $? -ne 0 ]; then
    echo
    echo "Error: Failed to generate visualization"
    exit 1
fi

echo
echo "Visualization generated successfully!"
echo "Check the generated PNG file in this directory"