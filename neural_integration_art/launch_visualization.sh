#!/bin/bash
echo "Neural Integration Art: Market Data & Somatic Feedback"
echo "Opening visualization in default browser..."
echo ""
echo "Files are located in: $(pwd)"
echo ""

# Try different commands to open the file based on the OS
if command -v xdg-open > /dev/null; then
    xdg-open index.html
elif command -v open > /dev/null; then
    open index.html
else
    echo "Please open index.html in your browser manually"
fi