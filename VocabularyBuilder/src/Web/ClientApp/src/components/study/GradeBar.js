import React from 'react';
import { Button } from 'reactstrap';
import { ReviewGrade } from './exerciseTypes';

const BUTTONS = [
  { grade: ReviewGrade.Again, label: 'Again', colour: 'danger', key: '1' },
  { grade: ReviewGrade.Hard, label: 'Hard', colour: 'warning', key: '2' },
  { grade: ReviewGrade.Good, label: 'Good', colour: 'success', key: '3' },
  { grade: ReviewGrade.Easy, label: 'Easy', colour: 'info', key: '4' }
];

/**
 * How the learner reports a recall that only they can see. Keys 1-4 match the buttons
 * left to right, so a session can be run without reaching for the mouse.
 */
export function GradeBar({ onGrade, disabled }) {
  return (
    <div className="d-flex gap-2 flex-wrap" data-testid="grade-bar">
      {BUTTONS.map(button => (
        <Button
          key={button.grade}
          color={button.colour}
          outline
          disabled={disabled}
          data-testid={`grade-${button.label.toLowerCase()}`}
          onClick={() => onGrade(button.grade)}
        >
          {button.label} <span className="text-muted small">({button.key})</span>
        </Button>
      ))}
    </div>
  );
}

export const GRADE_KEYS = BUTTONS.reduce((keys, button) => {
  keys[button.key] = button.grade;
  return keys;
}, {});
